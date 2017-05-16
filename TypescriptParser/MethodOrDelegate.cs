using System.ComponentModel;

namespace TypescriptParser
{
    public class MethodOrDelegate
    {
        public string Name;
        public bool Static;
        public bool Indexer;
        public Arguments Arguments;
        public Type ReturnType;
        public GenericDeclaration GenericDeclaration;
        public bool UsesNameAttribute;
        public bool Readonly;
        public string orgName;
        public Type ExplicitString;
    }
}