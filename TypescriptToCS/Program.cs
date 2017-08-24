using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TypescriptParser;

namespace TypescriptToCS
{
    public enum ConversionSoftware
    {
        DuoCode,
        Bridge
    }
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Starting...");
            string location = args.Length > 0 ? args[0] : Console.ReadLine();
            const string argStart = "--converter:";
            string converterV = args.FirstOrDefault(v => v.StartsWith(argStart));
            if (converterV == null)
                Console.WriteLine(@"What software would you like to convert for?
Currently Supported:
Bridge,
DuoCode");
            string converterValue = converterV == null ? Console.ReadLine() : converterV.Substring(argStart.Length);
            ConversionSoftware conversionSoftware = (ConversionSoftware)Enum.Parse(typeof(ConversionSoftware), converterValue);
            string file;
            Console.WriteLine("Reading file...");
            try
            {
                file = File.ReadAllText(location);
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                return;
            }
            var parser = new MTypescriptParser
            {
                ParseString = file
            };
            Console.WriteLine("Parsing...");
            parser.Parse();
            Namespace globalNamespace = parser.globalNamespace;
            var converter = new TypescriptToCSConverter(conversionSoftware);
            globalNamespace.classes.AddRange(new[]
            {
                new TypeDeclaration
                {
                    name = "NullType"
                },
                new TypeDeclaration
                {
                    name = "UndefinedType",
                    fields = new List<Field>
                    {
                        new Field
                        {
                            name = "Undefined",
                            @readonly = true,
                            @static = true,
                            template = "undefined",
                            type = new NamedType
                            {
                                Name = "UndefinedType"
                            }
                        }
                    }
                },
                new TypeDeclaration
                {
                    name = "VoidType",
                    implements = new List<TypescriptParser.Type>
                    {
                        new NamedType{Name = "UndefinedType"}
                    }
                },
                new TypeDeclaration
                {
                    name = "Symbol",
                    methods = new List<MethodOrDelegate>
                    {
                        new MethodOrDelegate
                        {
                            Arguments = new Arguments
                            {
                                Parameters = new List<Parameter>
                                {
                                    new Parameter
                                    {
                                        Name = "value",
                                        Type = new NamedType
                                        {
                                            Name = "string"
                                        }
                                    }
                                }
                            },
                            Name = "constructor"
                        },
                        new MethodOrDelegate
                        {
                            Name = "constructor"
                        }
                    }
                }
            });
            converter.globalNamespace = globalNamespace;
            //Console.WriteLine("Dotting...");
            //globalNamespace.ForeachTypeReference(v =>
            //{
            //    NamedType namedType = v as NamedType;
            //    if (string.IsNullOrEmpty(namedType?.Name))
            //        return;
            //    string[] dots = namedType.Name.Split('.');
            //    string[] preDots = new string[dots.Length - 1];
            //    Array.Copy(dots, preDots, dots.Length - 1);
            //    namedType.PreDots = preDots;
            //    namedType.Name = dots.Last();
            //});
            //Console.WriteLine("Merging...");
            //converter.MergeEnums(globalNamespace);
            //converter.RemoveAll(globalNamespace);
            Console.WriteLine("Referencing...");
            globalNamespace.ForeachTypeReference(converter.Reference);
            globalNamespace.ForeachType(v => converter.Reference(v.Type));
            Console.WriteLine("Unioning...");
            parser.Unions.ForEach(v =>
            {
                var typeA = v.Generics.Generic[0] as NamedType;
                var typeB = v.Generics.Generic[1] as NamedType;
                var typeDeclA = typeA.TypeDeclaration;
                var typeDeclB = typeB.TypeDeclaration;
                NamedType result;
                if (typeDeclA == null || typeDeclB == null)
                    result = new NamedType
                    {
                        Name = "object"
                    };
                else
                {
                    var shared = typeDeclA.FindSharedInterfaces(typeDeclB).Cast<TypescriptParser.Type>().ToList();
                    shared.Insert(0, new NamedType
                    {
                        Name = "Union",
                        Generics = new Generics
                        {
                            Generic = new List<TypescriptParser.Type>
                            {
                                typeA, typeB
                            }
                        }
                    });
                    string name = "Union_" + converter.Convert(typeA) + "_" + converter.Convert(typeB);
                    TypeDeclaration @ref = null;
                    globalNamespace.ForeachType(v2 => @ref = v2.name == name ? v2 : @ref);
                    bool create = @ref == null;
                    if (create)
                        @ref = new TypeDeclaration
                        {
                            name = name,
                            implements = shared,
                            IsUnion = true,
                            GenericDeclaration = new GenericDeclaration
                            {
                                Generics = new List<string> {"A", "B"}
                            }
                        };
                    v.Name = name;
                    v.TypeDeclaration = @ref;
                    if (create)
                        globalNamespace.classes.Add(@ref);
                }
            });
            Console.WriteLine("Renaming...");
            globalNamespace.ForeachType((Action<MethodOrDelegate>)(typeDeclaration =>
            {
                var delegatesFound = globalNamespace.FindType(new NamedType
                {
                    Name = typeDeclaration.Name,
                    Generics = new Generics
                    {
                        Generic = typeDeclaration.GenericDeclaration.Generics.ConvertAll<TypescriptParser.Type>(v2 => new NamedType { Name = v2 })
                    }
                }, converter.remove).delegatesFound;
                if (delegatesFound.Count > 1)
                    typeDeclaration.Name += "_" + delegatesFound.Count;
            }));
            Console.WriteLine("Removing duplicate fields...");
            converter.RemoveDuplicateFields();
            Console.WriteLine("Translating...");
            List<TypeDeclaration> toAdd = new List<TypeDeclaration>();
            globalNamespace.ForeachType(v => toAdd.AddRange(converter.Translate(v)));
            globalNamespace.ForeachType(v => converter.Cleanse(v.Type, v.GenericDeclaration, null));
            globalNamespace.classes.AddRange(toAdd);
            converter.remove.Clear();
            globalNamespace.ForeachType(converter.DeleteUnneededTypes);
            converter.RemoveAll(globalNamespace);
            Console.WriteLine("Writing C#...");
            //converter.ConvertUsingStatements(globalNamespace);
            converter.Result.Append(conversionSoftware == ConversionSoftware.Bridge ? "using Bridge;" : "using DuoCode.Runtime;");
            converter.Result.Append(
@"
//using number = System.Double;
//using any = Bridge.Union<System.Delegate, object>;
//using boolean = System.Boolean;
#pragma warning disable CS0626
#pragma warning disable CS0824
//[assembly: Convention(Notation.LowerCamelCase)]
");
            converter.Convert(globalNamespace, false);
            Console.WriteLine("Writing...");
            File.WriteAllText($"{Path.GetFileNameWithoutExtension(location)}.cs", converter.Result.ToString());
            Console.WriteLine("Exiting...");
        }
    }
}
