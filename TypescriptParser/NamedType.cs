using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TypescriptParser
{
    public class NamedType : Type
    {
        public string Name;
        public Generics Generics;
        public TypeDeclaration TypeDeclaration;
        public MethodOrDelegate ReferenceDelegates;
        public TTypeDeclaration ReferenceTTypes;
    }
}
