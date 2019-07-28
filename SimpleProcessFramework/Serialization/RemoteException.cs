using Spfx.Reflection;
using Spfx.Runtime.Exceptions;
using Spfx.Utilities;
using System;
using System.Runtime.Serialization;

namespace Spfx.Serialization
{
    public interface IRemoteExceptionInfo
    {
        void Rethrow();
        Exception RecreateException();
    }

    [DataContract]
    public class MarshalledRemoteExceptionInfo : IRemoteExceptionInfo
    {
        [DataMember]
        public SerializableException ExceptionObject { get; set; }

        public MarshalledRemoteExceptionInfo(SerializableException s)
        {
            ExceptionObject = s;
        }

        public Exception RecreateException()
        {
            return ExceptionObject;
        }

        public void Rethrow()
        {
            ExceptionObject.Rethrow();
        }
    }

    [DataContract]
    public class RemoteExceptionInfo : IRemoteExceptionInfo
    {
        [DataMember]
        public string StackTrace { get; private set; }

        [DataMember]
        public string Message { get; private set; }

        [DataMember]
        public ReflectedTypeInfo ExceptionType { get; private set; }

        private RemoteExceptionInfo(Exception ex)
        {
            StackTrace = ex.StackTrace;
            Message = ex.Message;
            ExceptionType = ex.GetType();
        }

        public static IRemoteExceptionInfo Create(Exception ex)
        {
            if (ex is SerializableException s)
                return new MarshalledRemoteExceptionInfo(s);

            return new RemoteExceptionInfo(ex);
        }

        public Exception RecreateException()
        {
            return new RemoteException(Message, ExceptionType, StackTrace);
        }

        public void Rethrow()
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
            OriginalExceptionType = originalException.GetType();
        }

        public RemoteException(string message, ReflectedTypeInfo exceptionType, string stackTrace)
            : base(message)
        {
            RemoteStackTrace = stackTrace;
            OriginalExceptionType = exceptionType;
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