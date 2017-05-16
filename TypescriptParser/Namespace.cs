using System;
using System.Collections.Generic;

namespace TypescriptParser
{
    public class Namespace
    {
        public List<Namespace> namespaces = new List<Namespace>();
        public List<TypeDeclaration> classes = new List<TypeDeclaration>();
        public List<MethodOrDelegate> delegates = new List<MethodOrDelegate>();
        public List<TTypeDeclaration> ttypes = new List<TTypeDeclaration>();
        public TypeDeclaration GlobalClass = new TypeDeclaration
        {
            name = "Global",
            fields = new List<Field>(),
            methods = new List<MethodOrDelegate>(),
            delegates = new List<MethodOrDelegate>(),
            nested = new List<TypeDeclaration>(),
            GenericDeclaration = new GenericDeclaration(),
            implements = new List<Type>(),
            kind = TypeDeclaration.Kind.Class,
            @static = true
        };
        public string name;
        public Namespace UpNamespace;

        public void ForeachType (Action<TypeDeclaration> @do)
        {
            namespaces?.ForEach(v => v.ForeachType(@do));
            classes?.ForEach(v => v.ForeachType(@do));
        }

        public void ForeachType (Action<TTypeDeclaration> @do)
        {
            namespaces?.ForEach(v => v.ForeachType(@do));
            ttypes?.ForEach(@do);
        }

        public void ForeachType(Action<MethodOrDelegate> @do)
        {
            namespaces?.ForEach(v => v.ForeachType(@do));
            delegates?.ForEach(@do);
            ForeachType(v => v.delegates?.ForEach(@do));
        }
        public List<TypeDeclaration> FindTypeName(string finding, HashSet<TypeDeclaration> remove)
        {
            List<TypeDeclaration> result = new List<TypeDeclaration>();
            ForeachType(v =>
            {
                if (v.name == finding && !remove.Contains(v))
                    result.Add(v);
            });
            return result;
        }
        public (List<TypeDeclaration> typesFound, List<TTypeDeclaration> tTypesFound, List<MethodOrDelegate> delegatesFound) FindType (NamedType finding, HashSet<TypeDeclaration> remove)
        {
            List<TypeDeclaration> typesFound = new List<TypeDeclaration>();
            List<TTypeDeclaration> tTypesFound = new List<TTypeDeclaration>();
            List<MethodOrDelegate> delegatesFound = new List<MethodOrDelegate>();
            ForeachType(@class =>
            {
                if (@class.GenericDeclaration?.Generics?.Count == finding.Generics?.Generic?.Count)
                    if (@class.name == finding.Name)
                        typesFound.Add(@class);
            });
            ForeachType(@delegate =>
            {
                if ((finding.Generics?.Generic?.Count ?? 0) == (@delegate.GenericDeclaration?.Generics?.Count ?? 0))
                    if (@delegate.Name == finding.Name)
                        delegatesFound.Add(@delegate);
            });
            ForeachType(tType =>
            {
                if (tType.Name == finding.Name)
                    tTypesFound.Add(tType);
            });
            return (typesFound, tTypesFound, delegatesFound);
        }
    }
}