using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TypescriptParser;

namespace TypescriptToCS
{
    public class TypescriptToCSConverter
    {
        public int Indent = 0;
        public StringBuilder Result = new StringBuilder();
        public ConversionSoftware conversionSoftware;
        public TypescriptToCSConverter (ConversionSoftware conversionSoftware)
        {
            this.conversionSoftware = conversionSoftware;
        }
        public void WriteNewLine ()
        {
            Result.AppendLine();
            for (int n = 0; n < Indent; n++)
                Result.Append('\t');
        }
        public const string MethodEmptyName = "Invoke";
        public const string FieldEmptyName = "EmptyString";
        //public string FindingIdentifier;
        //public List<TypeDeclaration> FoundIdentifiers = new List<TypeDeclaration>();
        //public void FindIdentifier (Namespace @namespace)
        //{
        //    @namespace.namespaces?.ForEach(FindIdentifier);
        //    @namespace.classes?.ForEach(FindIdentifier);
        //}
        //public void FindIdentifier (TypeDeclaration type)
        //{
        //    type.nested?.ForEach(FindIdentifier);
        //    if (type.name == FindingIdentifier && !remove.Contains(type))
        //        FoundIdentifiers.Add(type);
        //}
        public void DeleteUnneededTypes (TypeDeclaration type)
        {
            if (type.kind == TypeDeclaration.Kind.Enum && type.StringLiteralEnum)
                if (globalNamespace.FindTypeName(type.name, remove)?.Count > 1)
                    remove.Add(type);
        }
        public void Translate (MethodOrDelegate methodOrDelegate)
        {
            methodOrDelegate.orgName = methodOrDelegate.Name;
            methodOrDelegate.Name = ConvertToCSValidName(methodOrDelegate.Name, out bool nameAttribute, MethodEmptyName);
            methodOrDelegate.UsesNameAttribute = nameAttribute;
            if (methodOrDelegate.Arguments?.Parameters?.Count > 0)
            for (int n = 0; n < methodOrDelegate.Arguments.Parameters.Count; n++)
                methodOrDelegate.Arguments.Parameters[n].Name = ConvertToCSValidName(methodOrDelegate.Arguments.Parameters[n].Name, out bool _temp);
        }
        public Namespace globalNamespace;
        public List<TypeDeclaration> Translate (TypeDeclaration @class)
        {
            @class.orgName = @class.name;
            @class.name = Shorten(ConvertToCSValidName(@class.name, out @class.UsesNameAttribute));
            @class.UsesNameAttribute = @class.name != @class.orgName;
            List<TypeDeclaration> toAdd = new List<TypeDeclaration>();
            @class.nested?.ForEach(v => toAdd.AddRange(Translate(v)));
            @class.delegates?.ForEach(v => Translate(v, null));
            if (@class.kind == TypeDeclaration.Kind.Interface)
            {
                if (@class.nested != null)
                {
                    foreach (var nestedClass in @class.nested)
                        nestedClass.name = @class.name + "_" + nestedClass.name;
                    toAdd.AddRange(@class.nested);
                    @class.nested = null;
                }
                if (@class.delegates != null)
                {
                    foreach (var @delegate in @class.delegates)
                        @delegate.Name = @class.name + "_" + @delegate.Name;
                    globalNamespace.delegates.AddRange(@class.delegates);
                    @class.delegates = null;
                }
            }
            if (@class.methods != null)
                foreach (var method in @class.methods)
                    Translate(method, @class);
            if (@class.fields != null)
                foreach (var field in @class.fields)
                {
                    field.orgName = field.name;
                    field.name = ConvertToCSValidName(field.name, out bool nameAttribute);
                    field.UsesNameAttribute = nameAttribute;
                    Cleanse(field.type, @class.GenericDeclaration, @class);
                }
            return toAdd;
        }
        public void Translate (MethodOrDelegate methodOrDelegate, TypeDeclaration @class)
        {
            List<GenericDeclaration> gens = new List<GenericDeclaration>
            {
                @class?.GenericDeclaration,
                methodOrDelegate?.GenericDeclaration
            };
            Translate(methodOrDelegate, gens, @class);
        }
        public void Translate (MethodOrDelegate methodOrDelegate, List<GenericDeclaration> genericDeclarations, TypeDeclaration @class)
        {
            void CCleanse (TypescriptParser.Type type, bool returnType)
            {
                foreach (var gen in genericDeclarations)
                    Cleanse(type, gen, @class, returnType);
            }
            CCleanse(methodOrDelegate.ReturnType, true);
            if (methodOrDelegate.Arguments?.Parameters.Count > 0)
            foreach (var parameter in methodOrDelegate.Arguments.Parameters)
                CCleanse(parameter.Type, false);
            Translate(methodOrDelegate);
        }

        //public void ConvertUsingStatements (Namespace @namespace)
        //{
        //    @namespace.namespaces?.ForEach(ConvertUsingStatements);
        //    @namespace.ttypes?.ForEach(ConvertUsingStatements);
        //}
        //public void ConvertUsingStatements (TTypeDeclaration tType)
        //{
        //    Result.Append("using ");
        //    Result.Append(tType.Name);
        //    GenericDeclaration gen = null;
        //    if (tType.Type is NamedType namedType)
        //    {
        //        if (namedType.ReferenceTypes?.GenericDeclaration?.Generics?.Count > 0)
        //            gen = namedType.ReferenceTypes.GenericDeclaration;
        //        else if (namedType.ReferenceDelegates?.GenericDeclaration?.Generics?.Count > 0)
        //            gen = namedType.ReferenceDelegates.GenericDeclaration;
        //        if (gen != null)
        //        {
        //            tType.GenericDeclaration.Generics.AddRange(gen.Generics);
        //            foreach (var item in gen.Wheres)
        //                tType.GenericDeclaration.Wheres.Add(item.Key, item.Value);
        //        }
        //    }
        //    Convert1(tType.GenericDeclaration);
        //    Result.Append(" = ");
        //    Result.Append(Convert(tType.Type));
        //    if (gen != null)
        //        Convert1(gen);
        //    Result.Append(";");
        //    WriteNewLine();
        //}
        public void Convert(Namespace @namespace, bool displayHeader = true)
        {
            if (displayHeader)
            {
                WriteNewLine();
                Result.Append($"namespace {@namespace.name}");
                WriteNewLine();
                Result.Append('{');
                Indent++;
                WriteNewLine();
            }
            foreach (var @namespaceItem in @namespace.namespaces)
                Convert(@namespaceItem);
            foreach (var classItem in @namespace.classes)
                Convert(classItem);
            foreach (var @delegate in @namespace.delegates)
                Convert(@delegate);
            //foreach (var tType in @namespace.ttypes)
            //    Convert(tType);
            if (displayHeader)
            {
                Indent--;
                Result = Result.Remove(Result.Length - 1, 1);
                Result.Append('}');
                WriteNewLine();
                WriteNewLine();
            }
        }

        //public void Convert(TTypeDeclaration tType)
        //{
        //    Result.Append("public class ");
        //    Result.Append(tType.Name);
        //    Result.Append(" : ");
        //    Result.Append(Convert(tType.Type));
        //    Result.Append(" {}");
        //    WriteNewLine();
        //}

        static readonly string[] disallowedString =
        {
"abstract",
"as",
"base",
"bool",
"break",
"byte",
"case",
"catch",
"char",
"checked",
"class",
"const",
"continue",
"decimal",
"default",
"delegate",
"do",
"double",
"else",
"enum",
"event",
"explicit",
"extern",
"false",
"finally",
"fixed",
"float",
"for",
"foreach",
"goto",
"if",
"implicit",
"in",
"int",
"interface",
"internal",
"is",
"lock",
"long",
"namespace",
"new",
"null",
"object",
"operator",
"out",
"override",
"params",
"private",
"protected",
"public",
"readonly",
"ref",
"return",
"sbyte",
"sealed",
"short",
"sizeof",
"stackalloc",
"static",
"string",
"struct",
"switch",
"this",
"throw",
"true",
"try",
"typeof",
"uint",
"ulong",
"unchecked",
"unsafe",
"ushort",
"using",
"virtual",
"volatile",
"void",
"while"
        };
        const string allowedCSChars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ_1234567890";

        //public void MergeEnums (Namespace @namespace)
        //{
        //    @namespace.namespaces?.ForEach(MergeEnums);
        //    var toAdd = new List<TypeDeclaration>();
        //    @namespace.classes?.ForEach(v => toAdd.AddRange(MergeEnums(v)));
        //    @namespace.classes?.AddRange(toAdd);
        //}

        //public List<TypeDeclaration> MergeEnums (TypeDeclaration @class)
        //{
        //    List<TypeDeclaration> classes = new List<TypeDeclaration>();
        //    var toAdd = new List<TypeDeclaration>();
        //    @class.nested?.ForEach(v => toAdd.AddRange(MergeEnums(v)));
        //    @class.nested?.AddRange(toAdd);
        //    if (@class.kind == TypeDeclaration.Kind.Enum && !remove.Contains(@class))
        //    {
        //        Finding = new NamedType { Name = @class.name };
        //        Found.Clear();
        //        FoundB.Clear();
        //        FindIn(globalNamespace);
        //        foreach (var found in Found)
        //        {
        //            if (found == @class)
        //                continue;
        //            @class.fields.AddRange(found.fields);
        //            remove.Add(found);
        //        }
        //    }
        //    return classes;
        //}

        public void RemoveAll(TypeDeclaration @class, List<TypeDeclaration> classes)
        {
            var removeNested = new List<TypeDeclaration>();
            @class.nested?.ForEach(v => RemoveAll(v, removeNested));
            removeNested.ForEach(v => @class.nested?.Remove(v));
            if (remove.Contains(@class))
                classes.Add(@class);
        }

        public void RemoveAll(Namespace @namespace)
        {
            @namespace.namespaces?.ForEach(v => RemoveAll(v));
            var classRemove = new List<TypeDeclaration>();
            @namespace.classes?.ForEach(v => RemoveAll(v, classRemove));
            classRemove.ForEach(v => @namespace.classes.Remove(v));
        }

        public HashSet<TypeDeclaration> remove = new HashSet<TypeDeclaration>();

        public static string ConvertToCSValidName(string value, out bool nameAttribute, string emptyString = FieldEmptyName)
        {
            string org = value;
            if (value == "item")
                value = "_item";
            if (value?.Length != 0)
                if (char.IsNumber(value[0]))
                    value = '_' + value;
            char[] @new = value.ToCharArray();
            for (int n = 0; n < value.Length; n++)
                if (!allowedCSChars.Contains(value[n]))
                    @new[n] = '_';
            if (disallowedString.Contains(value))
                value = $"@{value}";
            if (string.IsNullOrEmpty(value))
                value = emptyString;
            if (value == org)
                value = new string(@new);
            nameAttribute = org != value;
            return value;
        }

        public void ConvertAsEnum(TypeDeclaration @enum)
        {
            Result.Append($"[External{(@enum.StringLiteralEnum ? ", Enum(Emit.StringNamePreserveCase)" : "")}]");
            WriteNewLine();
            Result.Append($"public enum {@enum.name}");
            WriteNewLine();
            Result.Append('{');
            Indent++;
            foreach (var value in @enum.fields)
            {
                WriteNewLine();
                if (value.UsesNameAttribute)
                    UseNameAttribute(value.orgName);
                Result.Append(value.name);
                Result.Append(',');
            }
            Indent--;
            WriteNewLine();
            Result.Append('}');
            WriteNewLine();
            WriteNewLine();
        }

        public string Cleanse(string value, GenericDeclaration genericDeclaration, TypeDeclaration @class, bool returnType)
        {
            if (genericDeclaration?.GenericsEquals != null)
                foreach (var item in genericDeclaration.GenericsEquals)
                    if (item.Key == value)
                    {
                        value = item.Value;
                        break;
                    }
            switch (value)
            {
                case "Union":
                    value = "Bridge.Union";
                    break;
                case "Tuple":
                    value = "System.Tuple";
                    break;
                case "string":
                    return "System.String";
                case "boolean":
                    return "System.Boolean";
                case "number":
                    return "System.Double";
                case "any":
                    return "Bridge.Union<System.Object, System.Delegate>";
                case "null":
                    return "NullType";
                case "undefined":
                    return "UndefinedType";
                case "void":
                    if (returnType)
                        break;
                    return "VoidType";
                case "never":
                    return returnType ? "void" : "Bridge.Union<System.Object, System.Delegate>";
                case "symbol":
                    return "Symbol";
            }
            return value;
        }

        //public List<TypeDeclaration> Found = new List<TypeDeclaration>();

        //public List<MethodOrDelegate> FoundB = new List<MethodOrDelegate>();
        

        //public List<TTypeDeclaration> FoundC = new List<TTypeDeclaration>();
        //public TypescriptParser.NamedType Finding;

        public void Reference (TypescriptParser.Type @type)
        {
            switch (@type)
            {
                case NamedType namedType:
                    if (namedType.TypeDeclaration != null || namedType.ReferenceDelegates != null)
                        return;
                    (   
                    List<TypeDeclaration> typesFound,
                    List<TTypeDeclaration> tTypesFound,
                    List<MethodOrDelegate> delegatesFound
                    )
                    =
                    globalNamespace.FindType(namedType, remove);
                    namedType.TypeDeclaration = typesFound.FirstOrDefault();
                    namedType.ReferenceDelegates = delegatesFound.FirstOrDefault();
                    namedType.ReferenceTTypes = tTypesFound.FirstOrDefault();
                    if (namedType.Generics?.Generic?.Count > 0)
                        foreach (var item in namedType.Generics.Generic)
                            Reference(item);
                    break;
            }
        }

        public void Reference (Arguments arguments)
        {
            arguments?.Parameters?.ForEach(v => Reference(v.Type));
        }

        public void Reference (TypeDeclaration typeDeclaration)
        {
            if (typeDeclaration.methods != null)
                foreach (var method in typeDeclaration.methods)
                {
                    Reference(method.Arguments);
                    Reference(method.ReturnType);
                }
            if (typeDeclaration.fields != null)
                foreach (var field in typeDeclaration.fields)
                    Reference(field.type);
            typeDeclaration.implements?.ForEach(Reference);
            typeDeclaration.delegates?.ForEach(Reference);
        }

        public void Reference (MethodOrDelegate @delegate)
        {
            Reference(@delegate.Arguments);
            Reference(@delegate.ReturnType);
        }

        public void Cleanse(TypescriptParser.Type type, GenericDeclaration genericDeclaration, TypeDeclaration @class, bool returnType = false)
        {
            if (type == null)
                return;
            switch (type)
            {
                case NamedType namedType:
                    if (namedType.ReferenceDelegates != null)
                        Translate(namedType.ReferenceDelegates, new List<GenericDeclaration> { genericDeclaration }, @class);
                    if (namedType.Name == "this")
                    {
                        namedType.Name = @class.name;
                        namedType.Generics.Generic.AddRange(@class.GenericDeclaration.Generics.ConvertAll(v => new NamedType { Name = v }));
                    }
                    namedType.Name = Cleanse(namedType.Name, genericDeclaration, @class, returnType);
                    if (namedType.Generics?.Generic != null)
                        foreach (var item in namedType.Generics.Generic)
                            Cleanse(item, genericDeclaration, @class);
                    break;
                default:
                    throw new NotImplementedException();
            }
        }
        public string Convert (TypescriptParser.Arguments arguments)
        {
            if (arguments?.Parameters?.Count > 0)
                return string.Join(", ", arguments.Parameters.ConvertAll(v => (v.Params ? "params " : "") + Convert(v.Type) + " " + v.Name + (v.Optional ? $" = default({Convert(v.Type)})" : "")));
            return string.Empty;
        }
        public void Convert(TypescriptParser.MethodOrDelegate @delegate)
        {
            Result.Append("[External]");
            WriteNewLine();
            Result.Append("public delegate ");
            Result.Append(Convert(@delegate.ReturnType));
            Result.Append(" ");
            Result.Append(@delegate.Name);
            Convert1(@delegate.GenericDeclaration);
            Result.Append(" (");
            Result.Append(Convert(@delegate.Arguments));
            Result.Append(");");
            WriteNewLine();
            WriteNewLine();
        }

        public string ConvertToUpperCase(string value)
        //{
        //    if (string.IsNullOrEmpty(value))
        //        return value;
        //    char[] newString = value.ToCharArray();
        //    //newString[0] = char.ToUpper(newString[0]);
        //    return new string(newString);
        //}
        => value;
        public void UseNameAttribute (string name)
        {
            if (conversionSoftware == ConversionSoftware.DuoCode)
                Result.Append($"[Js(Name=\"{name}\")]");
            else
                Result.Append($"[Name(\"{name}\")]");
            WriteNewLine();
        }

        public void Convert1 (GenericDeclaration genericDeclaration)
        {
            if (genericDeclaration.Generics?.Count > 0)
                Result.Append($"<{string.Join(", ", genericDeclaration.Generics)}>");
        }

        public void Convert2 (GenericDeclaration genericDeclaration)
        {
            List<string> whereStrings = new List<string>();
            foreach (var where in genericDeclaration.Wheres)
                if (where.Value.Count > 0)
                    whereStrings.Add($" where {where.Key} : {string.Join(", ", where.Value.ConvertAll(Convert))}");
            Result.Append(string.Join(", ", whereStrings));
        }

        public int count = 0;

        public string Shorten (string value)
        {
            return value.Length > 512 ? $"___{count++}" : value;
        }

        public string CalibrateSpace (string value)
        {
            StringBuilder result = new StringBuilder();
            bool space = false;
            bool @return = false;
            bool currentSpace = false;
            bool gone = false;
            const string lineReturns = "\r\n";
            for (int n = 0; n < value.Length; n++)
            {
                currentSpace = false;
                char item = value[n];
                space = (currentSpace = MTypescriptParser.spaceChars.Contains(item)) ? true : space;
                @return = lineReturns.Contains(item) ? true : @return;
                if (currentSpace)
                    continue;
                if (space)
                {
                    if (gone || @return)
                        result.Append(@return ? "\n" : " ");
                    space = false;
                    @return = false;
                }
                result.Append(item);
                gone = true;
            }
            return result.ToString();
        }

        public void Convert (Details details)
        {
            void NewLine()
            {
                WriteNewLine();
                Result.Append("/// ");
            }
            void WriteString (string toWrite)
            {
                string[] split = CalibrateSpace(toWrite).Split('\n');
                for (int n = 0; n < split.Length; n++)
                {
                    var item = split[n];
                    Result.Append(item);
                    NewLine();
                }
            }
            Result.Append("/// ");
            if (details.summary != null)
            {
                Result.Append("<summary>");
                NewLine();
                WriteString(details.summary);
                Result.Append("</summary>");
            }
            foreach (var item in details.paramDescription)
            {
                Result.Append("<param name=\"");
                Result.Append(item.Key);
                Result.Append("\">");
                NewLine();
                WriteString(item.Value);
                Result.Append("</param>");
            }
            if (details.returns != null)
            {
                Result.Append("<returns>");
                NewLine();
                WriteString(details.summary);
                Result.Append("</returns>");
            }
            WriteNewLine();
        }

        public void Convert(TypeDeclaration @class)
        {
            if (@class.details != null)
            {
                if (@class.details.paramDescription.Count > 0 || @class.details.summary != null || @class.details.returns != null)
                    Convert(@class.details);
            }
            if (@class.kind == TypeDeclaration.Kind.Enum)
            {
                ConvertAsEnum(@class);
                return;
            }
            Result.Append(conversionSoftware == ConversionSoftware.Bridge ? "[External]" : "[Js(Extern=true)]");
            WriteNewLine();
            if (@class.name == "Global")
            {
                Result.Append("[Name(\"Bridge.global\")]");
                WriteNewLine();
            }
            if (@class.UsesNameAttribute && !@class.name.StartsWith("Union_"))
                UseNameAttribute(@class.orgName);
            Result.Append($"public {(@class.@static ? "static " : "")}partial {@class.kind.ToString().ToLower()} {@class.name}");
            if (@class.GenericDeclaration?.Generics?.Count > 0)
                Convert1(@class.GenericDeclaration);
            if (@class.implements?.Count > 0)
                Result.Append($" : {string.Join(", ", @class.implements.ConvertAll(Convert))}");
            if (@class.GenericDeclaration?.Wheres?.Count > 0)
                Convert2(@class.GenericDeclaration);
            WriteNewLine();
            Result.Append('{');
            Indent++;
            WriteNewLine();
            if (@class.nested != null)
                foreach (var @classItem in @class.nested)
                    Convert(@classItem);
            if (@class.delegates != null)
                foreach (var @delegate in @class.delegates)
                    Convert(@delegate);
            List<Field> fields = new List<Field>();
            List<MethodOrDelegate> methods = new List<MethodOrDelegate>();
            if (@class.fields != null)
                fields.AddRange(@class.fields);
            if (@class.kind != TypeDeclaration.Kind.Interface)
                if (@class.implements != null)
                    foreach (var implement in @class.implements)
                    {
                        var namedType = implement as NamedType;
                        if (namedType.TypeDeclaration != null)
                            if (namedType.TypeDeclaration.kind == TypeDeclaration.Kind.Interface)
                            {
                                if (namedType.TypeDeclaration.fields != null)
                                    foreach (var field in namedType.TypeDeclaration.fields)
                                        //if (!fields.Any(v => v.name == field.name))
                                            fields.Add(new Field
                                            {
                                                name = field.name,
                                                type = field.type,
                                                ExplicitString = implement,
                                                optional = field.optional,
                                                orgName = field.orgName,
                                                @readonly = field.@readonly,
                                                @static = field.@static,
                                                template = field.template,
                                                UsesNameAttribute = field.UsesNameAttribute
                                            });
                                //fields.AddRange(namedType.TypeDeclaration.fields);
                                if (namedType.TypeDeclaration.methods != null)
                                    foreach (var method in namedType.TypeDeclaration.methods)
                                        //if (!methods.Any(v => v.Name == method.Name && ArgumentsEquals(v, method)))
                                            methods.Add(new MethodOrDelegate
                                            {
                                                Name = method.Name,
                                                GenericDeclaration = method.GenericDeclaration,
                                                Arguments = method.Arguments,
                                                Indexer = method.Indexer,
                                                orgName = method.orgName,
                                                UsesNameAttribute = method.UsesNameAttribute,
                                                Readonly = method.Readonly,
                                                Static = method.Static,
                                                ReturnType = method.ReturnType,
                                                ExplicitString = implement
                                            });
                            }
                    }
            foreach (var field in fields)
            {
                if (field.details?.paramDescription.Count > 0 || field.details?.returns != null || field.details?.summary != null)
                    Convert(field.details);
                if (field.UsesNameAttribute)
                    UseNameAttribute(field.orgName);
                string upperName = ConvertToUpperCase(field.name);
                Convert(upperName, field.name);
                if (field.ExplicitString == null)
                    Convert(@class.kind != TypeDeclaration.Kind.Interface, field.@static);
                else
                    Result.Append("extern ");
                Result.Append(Convert(field.type));
                Result.Append(" ");
                if (field.ExplicitString != null)
                {
                    Result.Append(Convert(field.ExplicitString));
                    Result.Append(".");
                }
                Result.Append(upperName);
                bool validTemplate = !string.IsNullOrEmpty(field.template);
                if (validTemplate)
                {
                    PrintTemplateProperty(field.template, field.@readonly);
                }
                else
                {
                    Result.Append(" { get;");
                    if (!field.@readonly)
                        Result.Append(" set;");
                    Result.Append(" }");
                }
                WriteNewLine();
            }
            if (@class.methods != null)
                methods.AddRange(@class.methods);
            foreach (var method in methods)
            {
                if (method.Details?.returns != null || method.Details?.summary != null || method.Details?.paramDescription?.Count > 0)
                    Convert(method.Details);
                string upperName = ConvertToUpperCase(method.Name);
                string template = null;
                if ((method.orgName == "new" || method.orgName == "") && string.IsNullOrEmpty(template))
                    template = (method.orgName == "" ? "" : "new ") +
                        "{this}" +
                        (method.Indexer ? '[' : '(') +
                    string.Join(", ",
                        method.Arguments.Parameters.ConvertAll(v => "{" + v.Name + "}")) +
                    (method.Indexer ? ']' : ')');
                else if (method.UsesNameAttribute)
                    UseNameAttribute(method.orgName);
                else Convert(upperName, method.Name);
                if (!string.IsNullOrEmpty(template) && !method.Indexer)
                {
                    Result.Append($"[Template(\"{template}\")]");
                    WriteNewLine();
                }
                if (method.ExplicitString == null)
                    Convert(@class.kind != TypeDeclaration.Kind.Interface, method.Static);
                else
                    Result.Append("extern ");
                Result.Append(method.Name == "constructor" ? @class.name : Convert(method.ReturnType));
                Result.Append(" ");
                if (method.ExplicitString != null)
                {
                    Result.Append(Convert(method.ExplicitString));
                    Result.Append(".");
                }
                if (method.Name != "constructor")
                    Result.Append(method.Indexer ? "this" : upperName);
                if (method.GenericDeclaration?.Generics?.Count > 0)
                    Convert1(method.GenericDeclaration);
                if (method.Name != "constructor")
                    Result.Append(" ");
                Result.Append(method.Indexer ? '[' : '(');
                Result.Append(Convert(method.Arguments));
                if (method.Indexer)
                {
                    Result.Append(']');
                    PrintTemplateProperty(template, method.Readonly);
                }
                else
                {
                    Result.Append(")");
                    if (method.GenericDeclaration?.Wheres?.Count > 0 && method.ExplicitString == null)
                        Convert2(method.GenericDeclaration);
                    Result.Append(";");
                }
                WriteNewLine();
            }
            Result.Remove(Result.Length - 1, 1);
            Indent--;
            Result.Append('}');
            WriteNewLine();
            WriteNewLine();
        }

        public bool ArgumentsEquals(MethodOrDelegate a, MethodOrDelegate b)
        {
            if (a.Arguments.Parameters.Count != b.Arguments.Parameters.Count)
                return false;
            for (int n = 0; n < a.Arguments.Parameters.Count; n++)
            {
                var param = a.Arguments.Parameters[n];
                var paramB = b.Arguments.Parameters[n];
                if (!TypescriptParser.Type.Equals(param.Type, paramB.Type))
                    return false;
            }
            return true;
        }

        public void PrintTemplateProperty (string template, bool @readonly)
        {
            WriteNewLine();
            Result.Append("{");
            Indent++;
            WriteNewLine();
            PrintTemplate(template);
            Result.Append("get;");
            WriteNewLine();
            if (!@readonly)
            {
                PrintTemplate(template);
                Result.Append("set;");
                Indent--;
                WriteNewLine();
            }
            else
            {
                Indent--;
                Result = Result.Remove(Result.Length - 1, 1);
            }
            Result.Append("}");
        }

        public bool PrintTemplate (string template)
        {
            bool result;
            if (result = !string.IsNullOrEmpty(template))
            {
                Result.Append($"[Template(\"{template}\")]");
                WriteNewLine();
            }
            return result;
        }

        public void Convert (string upperName, string name)
        {
            //if (upperName == name)
            //{
            //    Result.Append("[Convention(Notation.UpperCamelCase)]");
            //    WriteNewLine();
            //}
        }

        public void Convert (bool isNotInterface, bool isStatic)
        {
            if (isNotInterface)
                Result.Append("public extern ");
            if (isStatic)
                Result.Append("static ");
        }

        public string Convert (TypescriptParser.Type type)
        {
            string result = string.Empty;
            switch (type)
            {
                case NamedType namedType:
                    string name = namedType.Name;
                    if (name == "Array`")
                        return Convert(namedType.Generics.Generic[0]) + "[]";
                    if (namedType.TypeDeclaration != null)
                        name = namedType.TypeDeclaration.name;
                    if (namedType.ReferenceDelegates != null)
                        name = namedType.ReferenceDelegates.Name;
                    if (namedType.ReferenceTTypes != null)
                        return Convert(namedType.ReferenceTTypes.Type);
                    result += name;
                    if (namedType.Generics?.Generic.Count > 0)
                    {
                        result += "<";
                        result += string.Join(", ", namedType.Generics.Generic.ConvertAll(Convert));
                        result += ">";
                    }
                    break;
            }
            return result;
        }
    }
}
