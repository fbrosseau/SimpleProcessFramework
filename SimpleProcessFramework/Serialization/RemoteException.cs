using Spfx.Reflection;
using System;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace Spfx.Serialization
{
    [DataContract]
    public class RemoteExceptionInfo
    {
        [DataMember]
        public string StackTrace { get; private set; }

        [DataMember]
        public string Message { get; private set; }

        [DataMember]
        public ReflectedTypeInfo ExceptionType { get; private set; }

        public RemoteExceptionInfo(Exception ex)
        {
            StackTrace = ex.StackTrace;
            Message = ex.Message;
            ExceptionType = ex.GetType();
        }

        internal Exception RecreateException()
        {
            return new RemoteException(Message, ExceptionType, StackTrace);
        }

        public RemoteException Rethrow()
        {
            throw RecreateException();
        }
    }

    public class RemoteException : Exception
    {
        public string RemoteCallstack { get; }
        public ReflectedTypeInfo OriginalExceptionType { get; }
        public string RemoteStackTrace { get; }

        public RemoteException(Exception originalException)
            : base(originalException.Message, originalException as RemoteException)
        {
            RemoteCallstack = originalException.StackTrace;
        }

        public RemoteException(string message, ReflectedTypeInfo exceptionType, string stackTrace)
            : base(message)
        {
            RemoteStackTrace = stackTrace;
        }

        public override string StackTrace
        {
            get
            {
                if (!string.IsNullOrWhiteSpace(RemoteStackTrace))
                    return base.StackTrace + "\r\n---Remote stacktrace---\r\n" + RemoteStackTrace;
                return base.StackTrace;
            }
        }
    }
}