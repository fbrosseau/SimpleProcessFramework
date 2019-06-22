using System;
using System.Collections.Generic;
using System.Text;

namespace Spfx.Runtime.Exceptions
{
    public class CodeGenerationFailedException : Exception
    {
        public CodeGenerationFailedException(string message, Exception innerEx)
            : base(message, innerEx)
        {
        }
    }
}
