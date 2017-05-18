namespace TypescriptParser
{
    public class Field
    {
        public Type type;
        public string name;
        public bool @readonly;
        public bool @static;
        public bool optional;
        public bool UsesNameAttribute;
        public string orgName;
        public string template;
        public Type ExplicitString;
        public Details details;
    }
}