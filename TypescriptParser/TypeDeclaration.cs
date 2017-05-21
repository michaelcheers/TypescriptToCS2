using System;
using System.Collections.Generic;
using System.Linq;

namespace TypescriptParser
{
    public class TypeDeclaration
    {
        public bool IsUnion;
        public List<Field> fields;
        public List<MethodOrDelegate> methods;
        public List<TypeDeclaration> nested;
        public List<MethodOrDelegate> delegates;
        public List<Type> implements;
        public bool StringLiteralEnum;
        public GenericDeclaration GenericDeclaration;
        public Details details;
        public string name;
        public string orgName;
        public Kind kind;
        public bool @static;
        public bool UsesNameAttribute;

        public enum Kind
        {
            Class,
            Interface,
            Struct,
            Enum
        }

        public void ForeachType (Action<TypeDeclaration> @do)
        {
            nested?.ForEach(v => v.ForeachType(@do));
            @do(this);
        }
        public (List<Field>, List<MethodOrDelegate>) FindClassMembers(string name)
        {
            List<Field> fields = new List<Field>();
            List<MethodOrDelegate> methods = new List<MethodOrDelegate>();
            if (this.fields != null)
                fields.AddRange(this.fields?.Where(field => field.name == name));
            if (this.methods != null)
                methods.AddRange(this.methods.Where(method => method.Name == name));
            return (fields, methods);
        }

        public List<NamedType> FindSharedInterfaces (TypeDeclaration b)
        {
            bool IsObjectOrInterface (NamedType v) =>
                v.TypeDeclaration?.kind == Kind.Interface;
            var ancestorsA = AncestorTypes.Where(IsObjectOrInterface);
            var ancestorsB = b.AncestorTypes.Where(IsObjectOrInterface);
            List<NamedType> intersected = new List<NamedType>();
            foreach (var ancestor in ancestorsA)
            {
                foreach (var ancestorB in ancestorsB)
                {
                    if (ancestor.Name == ancestorB.Name)
                    {
                        intersected.Add(ancestor);
                        break;
                    }
                }
            }
            return intersected;
        }

        public List<NamedType> AncestorTypes
        {
            get
            {
                List<NamedType> result = new List<NamedType>();
                result.Add(new NamedType
                {
                    Name = name,
                    TypeDeclaration = this
                });
                implements?.ForEach(v =>
                {
                    if (v is NamedType namedType)
                        if (namedType.TypeDeclaration != null)
                            result.AddRange(namedType.TypeDeclaration.AncestorTypes);
                });
                return result;
            }
        }
    }
}