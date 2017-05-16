using System.Linq;

namespace TypescriptParser
{
    public class Type
    {
        public static bool Equals (Type a, Type b)
        {
            if (a is NamedType namedTypeA)
            {
                if (!(b is NamedType namedTypeB))
                    return false;
                if (namedTypeA.Name != namedTypeB.Name)
                    return false;
                int count = (namedTypeA.Generics?.Generic?.Count ?? 0);
                if (count != (namedTypeB.Generics?.Generic?.Count ?? 0))
                    return false;
                if (count == 0)
                    return true;
                if (!namedTypeA.Generics.Generic.SequenceEqual(namedTypeB.Generics.Generic))
                    return false;
            }
            return true;
        }
    }
}