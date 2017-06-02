using System;
using System.Collections.Generic;

namespace TypescriptParser
{
    public class Arguments
    {
        public List<Parameter> Parameters;

        public void FindTypeReference (Action<Type> toRun)
        {
            Parameters?.ForEach(v => toRun(v.Type));
        }
    }
}