using System;

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
