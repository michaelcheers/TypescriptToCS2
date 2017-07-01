using System;
using System.Collections.Generic;
using System.Linq;

namespace TypescriptParser
{
    public class MTypescriptParser
    {
        public Namespace globalNamespace;
        public int index;
        public Namespace currentNamespace;
        public TypeDeclaration currentClass;
        public string ParseString;
        public char Current => CharAt(index);
        public Stack<int> restorePoints = new Stack<int>();

        public void CreateRestorePoint()
        {
            restorePoints.Push(index);
        }

        public void Restore ()
        {
            index = restorePoints.Pop();
        }

        public bool CurrentIs(char value)
        {
            SkipEmpty();
            return Current == value;
        }

        public bool GoForwardIf(char value)
        {
            SkipEmpty();
            bool result = CurrentIs(value);
            if (result)
                GoForwardOne();
            return result;
        }

        public bool CurrentIs(string value)
        {
            SkipEmpty();
            for (int n = 0; n < value.Length; n++)
                if (CharAt(index + n) != (value[n]))
                    return false;
            return true;
        }

        public bool GoForwardIf(string value)
        {
            SkipEmpty();
            for (int n = 0; n < value.Length; n++)
            {
                if (!CurrentIs(value[n]))
                {
                    GoForward(-n);
                    return false;
                }
                GoForwardOne();
            }
            return true;
        }

        public Details ParseComment()
        {
            if (LastComment == null)
                return new Details();
            MTypescriptParser parser = new MTypescriptParser();
            parser.ParseString = LastComment;
            Details result = new Details();
            string header = null;
            string paramName = null;
            while (!parser.CurrentIs('\0'))
            {
                parser.GoForwardIf('*');
                if (!parser.GoForwardIf('@'))
                {
                    string g = string.Empty;
                    if (!parser.CurrentIs('\0'))
                    {
                        parser.CreateRestorePoint();
                        parser.InSkipEmpty = true;
                        parser.SkipUntil(LastComment.Substring(parser.index).Contains('@') ? '@' : '\0');
                        parser.InSkipEmpty = false;
                        while (parser.CurrentIs('\0'))
                            parser.index--;
                        int oldIndex = parser.restorePoints.Pop();
                        g = LastComment.Substring(oldIndex, parser.index - oldIndex - 1).Replace("*", "");
                    }
                    switch (header)
                    {
                        case "param":
                            result.paramDescription.Add(paramName, g);
                            break;
                        case "returns":
                        case "return":
                            result.returns = g;
                            break;
                        case null:
                            result.summary = g;
                            break;
                    }
                }
                parser.GoForwardIf('*');
                header = parser.GetWord();
                paramName = null;
                if (parser.GoForwardIf('{'))
                    parser.SkipUntil('}');
                if (header == "param")
                    paramName = parser.GetWord();
            }
            return result;
        }

        public char CharAt (int index_) => index_ < 0 || index_ >= ParseString.Length ? '\0' : ParseString[index_];
        public void GoForwardOne() => GoForward(1);
        public void GoForward(int length) => index += length;
        public const string spaceChars = " \t\r\n";
        public const string wordChars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ_$1234567890.";

        public string GetWord()
        {
            string result = "";
            SkipEmpty();
            while (wordChars.Contains(Current))
            {
                result += Current;
                GoForwardOne();
            }
            return result;
        }

        private void SkipUntil (char value)
        {
            while (!CurrentIs(value)) GoForwardOne();
            GoForwardOne();
        }

        private void SkipUntil(string value)
        {
            while (!CurrentIs(value) && !CurrentIs('\0')) GoForwardOne();
            GoForward(value.Length);
        }

        public string LastComment;

        private bool SkipEmpty(bool skipComments = true)
        {
            if (InSkipEmpty)
                return false;
            InSkipEmpty = true;
            bool skipped = false;
            while (spaceChars.Contains(Current) || (CurrentIs('/') && skipComments))
            {
                if (GoForwardIf('/'))
                {
                    if (GoForwardIf('/'))
                        SkipUntil('\n');
                    else if (GoForwardIf('*'))
                    {
                        bool docs;
                        if (docs = CurrentIs("*"))
                            CreateRestorePoint();
                        SkipUntil("*/");
                        if (docs)
                        {
                            int orgIndex = restorePoints.Pop();
                            int nextIndex = index - 2;
                            string subString = ParseString.Substring(orgIndex, nextIndex - orgIndex + 1);
                            LastComment = subString;
                        }
                    }
                    else
                        throw new Exception();
                }
                else
                    GoForwardOne();
                skipped = true;
            }
            InSkipEmpty = false;
            return skipped;
        }
        string FunctionName;
        bool InSkipEmpty;

        public TypeDeclaration ParseClass(string name, GenericDeclaration genericDeclaration, List<Type> implements, bool @interface)
        {
            if (!GoForwardIf('{'))
                throw new Exception();
            currentClass = new TypeDeclaration
            {
                fields = new List<Field>(),
                methods = new List<MethodOrDelegate>(),
                nested = new List<TypeDeclaration>(),
                delegates = new List<MethodOrDelegate>(),
                implements = implements,
                GenericDeclaration = genericDeclaration,
                name = name,
                kind = @interface ? TypeDeclaration.Kind.Interface : TypeDeclaration.Kind.Class,
                upperNamespace = currentNamespace
            };
            while (true)
                if (ParseClassLine())
                    return currentClass;
        }
        public bool ParseClassLine ()
        {
            bool @readonly = false;
            bool @static = false;
            bool indexer = false;
            Back:
            bool quoted = CurrentIs('"') || CurrentIs('\'');
            string word;
            if (!quoted)
                word = GetWord();
            else
            {
                int oldIdx = index;
                GoForwardUntilEndBracket('"', '\'', '"', '\'');
                word = '\x1' + ParseString.Substring(oldIdx + 1, index - 2 - oldIdx);
            }
            switch (word)
            {
                case "":
                    if (GoForwardIf('}'))
                        return true;
                    else if (CurrentIs('(') || CurrentIs('<'))
                        goto default;
                    else if (GoForwardIf('['))
                    {
                        indexer = true;
                        goto default;
                    }
                    else
                        throw new Exception();
                case "static":
                case "let":
                case "var":
                    if (CurrentIs(':') || CurrentIs('(') || CurrentIs('?'))
                        goto default;
                    @static = true;
                    goto Back;
                case "declare":
                    if (CurrentIs(':') || CurrentIs('(') || CurrentIs('?'))
                        goto default;
                    goto Back;
                case "readonly":
                    if (CurrentIs(':') || CurrentIs('(') || CurrentIs('?'))
                        goto default;
                    @readonly = true;
                    goto Back;
                case "const":
                    if (CurrentIs(':') || CurrentIs('('))
                        goto default;
                    @static = true;
                    goto case "readonly";
                case "function":
                    @static = true;
                    goto Back;
                default:
                    if (!string.IsNullOrEmpty(word))
                        if (word[0] == '\x1')
                            word = word.Substring(1);
                    bool optional = GoForwardIf('?');
                    var generics = ParseGenericDeclaration();
                    Details details = ParseComment();
                    if (GoForwardIf('(') || indexer)
                    {
                        Inherit.Clear();
                        Inherit.Add(generics);
                        Inherit.Add(currentClass.GenericDeclaration);
                        FunctionName = word;
                        var arguments = ParseArguments();
                        Type returnType;
                        if (!GoForwardIf(':'))
                        {
                            returnType = new NamedType
                            {
                                Name = "void"
                            };
                            goto After;
                        }
                        TypeDeclName = word + "_ReturnType";
                        returnType = ParseType();
                        After:
                        currentClass.methods.Add(new MethodOrDelegate
                        {
                            Arguments = arguments,
                            Name = word,
                            ReturnType = returnType,
                            Static = @static,
                            Indexer = indexer,
                            GenericDeclaration = generics,
                            Readonly = @readonly,
                            Details = details
                        });
                    }
                    else if (GoForwardIf(':'))
                    {
                        TypeDeclName = word + "_Type";
                        Inherit.Clear();
                        Inherit.Add(currentClass.GenericDeclaration);
                        currentClass.fields.Add(new Field
                        {
                            name = word,
                            type = ParseType(),
                            @readonly = @readonly,
                            @static = @static,
                            optional = optional,
                            details = details
                        });
                    }
                    else throw new Exception();
                    GoForwardIf(',');
                    GoForwardIf(';');
                    //if (!GoForwardIf(';'))
                    //    throw new Exception();
                    break;
            }
            LastComment = null;
            return false;
        }
        string TypeDeclName;

        public Arguments ParseArguments()
        {
            Arguments result = new Arguments
            {
                Parameters = new List<Parameter>()
            };
            while (true)
            {
                bool optional = false;
                bool @params = GoForwardIf("...");
                string name = GetWord();
                if (GoForwardIf(')') || GoForwardIf(']'))
                    break;
                if (GoForwardIf('?'))
                    optional = true;
                if (!GoForwardIf(':'))
                    throw new Exception();
                TypeDeclName = $"{FunctionName}_Param_{name}";
                result.Parameters.Add(new Parameter
                {
                    Name = name,
                    Optional = optional,
                    Params = @params,
                    Type = ParseType()
                });
                GoForwardIf(',');
            }
            return result;
        }

        public List<NamedType> Unions = new List<NamedType>();

        public Type ParseType()
        {
            Type typeResult = ParseTypeLevel1();
            if (GoForwardIf('|'))
            {
                TypeDeclName += "_UnionRight";
                var type2 = ParseType();
                typeResult = new NamedType
                {
                    Name = "Union",
                    Generics = new Generics
                    {
                        Generic = new List<Type>
                        {
                            typeResult,
                            type2
                        }
                    }
                };
                Unions.Add(typeResult as NamedType);
            }
            else if (GoForwardIf('&'))
            {
                TypeDeclName += "_IntersectionRight";
                typeResult = new NamedType
                {
                    Name = "Intersection",
                    Generics = new Generics
                    {
                        Generic = new List<Type>
                        {
                            typeResult,
                            ParseType()
                        }
                    }
                };
            }
            return typeResult;
        }

        public Type ParseTypeLevel1 ()
        {
            Type result = ParseTypeLevel2();
            while (GoForwardIf('['))
            {
                if (!GoForwardIf(']'))
                    throw new Exception();
                result = new NamedType
                {
                    Name = "Array`",
                    Generics = new Generics
                    {
                        Generic = new List<Type>
                        {
                            result
                        }
                    }
                };
            }
            return result;
        }

        public Type CreateStringLiteralType (string name)
        {
            string actName = TypeDeclName + "_" + name.Replace('.', '_');
            TypeDeclaration @class;
            (currentClass?.nested ?? currentNamespace.classes).Add(@class = new TypeDeclaration
            {
                kind = TypeDeclaration.Kind.Enum,
                IsStringLiteralEnum = true,
                name = actName,
                fields = new List<Field>
                {
                    new Field
                    {
                        name = name
                    }
                },
                upperNamespace = currentNamespace
            });
            return new NamedType
            {
                Name = actName,
                TypeDeclaration = @class
            };
        }

        public Type ParseTypeLevel2 ()
        {
            bool @typeof = false;
            if (GoForwardIf("typeof"))
                @typeof = true;
            Type resultType = ParseTypeLevel3();
            if (@typeof)
            {
                if (!(resultType is NamedType namedType))
                    throw new Exception();
                return CreateStringLiteralType(namedType.Name);
            }
            return resultType;
        }
        public List<GenericDeclaration> Inherit = new List<GenericDeclaration>();

        public Type ParseTypeLevel3 ()
        {
            Type resultType = null;
            string word = GetWord();
            switch (word)
            {
                case "":
                case "new":
                    if (CurrentIs('(') || CurrentIs('<'))
                    {
                        bool function = true;
                        if (!CurrentIs('<'))
                        {
                            CreateRestorePoint();
                            GoForwardUntilEndBracket('(', ')');
                            function = GoForwardIf("=>");
                            Restore();
                        }
                        if (function)
                        {
                            string org = TypeDeclName;
                            GenericDeclaration genericDeclaration = ParseGenericDeclaration();
                            var newGen = new HashSet<string>();
                            foreach (var item in Inherit)
                                item.Generics.ForEach(v => newGen.Add(v));
                            genericDeclaration.Generics.AddRange(newGen);
                            GoForwardOne();
                            Inherit.Add(genericDeclaration);
                            var arguments = ParseArguments();
                            Inherit.RemoveAt(Inherit.Count - 1);
                            if (!GoForwardIf("=>"))
                                throw new Exception();
                            org += "_ReturnType";
                            TypeDeclName = org;
                            var returnType = ParseType();
                            MethodOrDelegate newType;
                            (currentClass?.delegates ?? currentNamespace.delegates).Add(newType = new MethodOrDelegate
                            {
                                Arguments = arguments,
                                ReturnType = returnType,
                                Name = TypeDeclName,
                                GenericDeclaration = genericDeclaration
                            });
                            resultType = new NamedType
                            {
                                Name = org,
                                ReferenceDelegates = newType,
                                Generics = new Generics
                                {
                                    Generic = newGen.ToList().ConvertAll<Type>(v => new NamedType
                                    {
                                        Name = v
                                    })
                                }
                            };
                        }
                        else
                        {
                            GoForwardOne();
                            resultType = ParseType();
                            if (!GoForwardIf(')'))
                                throw new Exception();
                        }
                        return resultType;
                    }
                    else if (CurrentIs('{'))
                    {
                        string org = TypeDeclName;
                        TypeDeclaration created;
                        var old = currentClass;
                        (currentClass?.nested ?? currentNamespace.classes).Add(created = ParseClass(org, new GenericDeclaration(), new List<Type>(), true));
                        currentClass = old;
                        return new NamedType
                        {
                            Generics = new Generics
                            {
                                Generic = new List<Type>()
                            },
                            Name = org,
                            TypeDeclaration = created
                        };
                    }
                    else if (GoForwardIf('['))
                    {
                        List<Type> types = new List<Type>();
                        string org = TypeDeclName;
                        int n = 0;
                        do
                        {
                            TypeDeclName = org + "_TupleParam" + n;
                            types.Add(ParseType());
                            ++n;
                        }
                        while (GoForwardIf(','));
                        if (!GoForwardIf(']'))
                            throw new Exception();
                        return new NamedType
                        {
                            Generics = new Generics
                            {
                                Generic = types
                            },
                            Name = "Tuple"
                        };
                    }
                    else if (CurrentIs('"') || CurrentIs('\''))
                    {
                        var oldIdx = index;
                        GoForwardUntilEndBracket('"', '\'', '"', '\'');
                        int gIdx = index - 2 - oldIdx;
                        var stringLiteral = gIdx <= 0 ? string.Empty : ParseString.Substring(oldIdx + 1, gIdx);
                        return CreateStringLiteralType(stringLiteral);
                    }//
                    throw new NotImplementedException();
                default:
                    List<string> dotsSplit = word.Split('.').ToList();
                    string shortName = dotsSplit[dotsSplit.Count - 1];
                    dotsSplit.RemoveAt(dotsSplit.Count - 1);
                    List<Type> generics = new List<Type>();
                    if (GoForwardIf('<'))
                    {
                        do
                            generics.Add(ParseType());
                        while (GoForwardIf(','));
                        if (!GoForwardIf('>'))
                        throw new Exception();
                    }
                    if (GoForwardIf("is"))
                    {
                        shortName = "boolean";
                        dotsSplit.Clear();
                        ParseType();
                    }
                    return new NamedType
                    {
                        Name = shortName,
                        PreDots = dotsSplit.ToArray(),
                        Generics = new Generics
                        {
                            Generic = generics
                        }
                    };
            }
        }

        public void GoForwardUntilEndBracket (params char[] inputArray)
        {
            List<char> asList = new List<char>(inputArray);
            char[] open = new char[inputArray.Length / 2];
            Array.Copy(inputArray, open, inputArray.Length / 2);
            asList.RemoveRange(0, inputArray.Length / 2);
            char[] closed = asList.ToArray();
            if (open.Contains('"'))
                InSkipEmpty = true;
            int parantheses = 0;
            do
            {
                if (open.Contains(Current) && (!open.All(v => closed.Contains(v)) || parantheses == 0))
                    parantheses++;
                else if (closed.Contains(Current))
                    parantheses--;
                GoForwardOne();
            } while (parantheses != 0);
            InSkipEmpty = false;
        }
        
        public GenericDeclaration ParseGenericDeclaration ()
        {
            var genericDeclaration = new GenericDeclaration();
            if (GoForwardIf('<'))
            {
                while (!GoForwardIf('>'))
                {
                    string genericName = GetWord();
                    if (GoForwardIf('='))
                    {
                        string genericName2 = GetWord();
                        genericDeclaration.GenericsEquals.Add(genericName, genericName2);
                    }
                    else
                    {
                        genericDeclaration.Generics.Add(genericName);
                        if (!genericDeclaration.Wheres.ContainsKey(genericName))
                            genericDeclaration.Wheres.Add(genericName, new List<Type>());
                        while (GoForwardIf("extends") || GoForwardIf("implements"))
                            genericDeclaration.Wheres[genericName].Add(ParseType());
                    }
                    GoForwardIf(',');
                }
            }
            return genericDeclaration;
        }

        public void Parse()
        {
            globalNamespace = currentNamespace = new Namespace
            {
                name = "Global"
            };
            globalNamespace.classes.Add(globalNamespace.GlobalClass);
            while (true)
            {
                Back:
                if (CurrentIs('\0'))
                    return;
                string word = GetWord();
                switch (word)
                {
                    case "namespace":
                    case "module":
                        string name = GetWord();
                        if (!GoForwardIf('{'))
                            throw new Exception();
                        var @namespace = new Namespace
                        {
                            name = name,
                            UpNamespace = currentNamespace
                        };
                        @namespace.classes.Add(@namespace.GlobalClass);
                        currentNamespace.namespaces.Add(@namespace);
                        currentNamespace = @namespace;
                        break;
                    case "declare":
                    case "export":
                        goto Back;
                    case "const":
                    case "function":
                    case "var":
                    case "let":
                        GoForward(-word.Length);
                        currentClass = currentNamespace.GlobalClass;
                        if (ParseClassLine())
                            throw new Exception();
                        break;
                    case "type":
                        currentClass = null;
                        string typeName = TypeDeclName = GetWord();
                        bool generic = CurrentIs('<');
                        Inherit.Clear();
                        GenericDeclaration genericDeclaration = ParseGenericDeclaration();
                        if (!GoForwardIf('='))
                            throw new Exception();
                        Inherit.Add(genericDeclaration);
                        var type = ParseType();
                        bool created = false;
                        if (type is NamedType namedType)
                            if (namedType.Name == typeName)
                                created = true;
                        if (generic && created)
                            currentNamespace.classes.Last(v => v.name == typeName).GenericDeclaration = genericDeclaration;
                        if (!created)
                            currentNamespace.ttypes.Add(new TTypeDeclaration
                            {
                                Name = typeName,
                                Type = type,
                                GenericDeclaration = genericDeclaration
                            });
                        if (!GoForwardIf(';'))
                            throw new Exception();
                        break;
                    case "interface":
                    case "class":
                        var interfaceName = GetWord();
                        var implements = new List<Type>();
                        var genericDeclaration2 = ParseGenericDeclaration();
                        Inherit.Clear();
                        Inherit.Add(genericDeclaration2);
                        while (GoForwardIf("extends") || GoForwardIf("implements"))
                        {
                            do implements.Add(ParseType());
                            while (GoForwardIf(','));
                        }
                        Details details = ParseComment();
                        TypeDeclaration @class;
                        currentNamespace.classes.Add(@class = ParseClass(interfaceName, genericDeclaration2, implements, word == "interface"));
                        @class.details = details;
                        break;
                    case "enum":
                        string enumName = GetWord();
                        var enumDecl = new TypeDeclaration
                        {
                            name = enumName,
                            fields = new List<Field>(),
                            kind = TypeDeclaration.Kind.Enum,
                            upperNamespace = currentNamespace
                        };
                        if (!GoForwardIf('{'))
                            throw new Exception();
                        while (!GoForwardIf('}'))
                        {
                            var key = GetWord();
                            if (string.IsNullOrWhiteSpace(key))
                                throw new Exception();
                            enumDecl.fields.Add(new Field
                            {
                                @readonly = true,
                                @static = true,
                                @name = key,
                                type = new NamedType
                                {
                                    Name = currentClass.name
                                }
                            });
                            GoForwardIf(',');
                        }
                        currentNamespace.classes.Add(enumDecl);
                        break;
                    case "":
                        if (GoForwardIf('}'))
                            currentNamespace = currentNamespace.UpNamespace;
                        break;
                }
                LastComment = null;
            }
        }
    }
}
