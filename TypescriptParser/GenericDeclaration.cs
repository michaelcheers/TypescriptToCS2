using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TypescriptParser
{
    public class GenericDeclaration
    {
        public List<string> Generics = new List<string>();
        public Dictionary<string, string> GenericsEquals = new Dictionary<string, string>();
        public Dictionary<string, List<Type>> Wheres = new Dictionary<string, List<Type>>();
    }
}
