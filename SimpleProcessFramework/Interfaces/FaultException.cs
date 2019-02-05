using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Text;

namespace Spfx.Interfaces
{
    [DataContract]
    public class FaultException<TFault> : Exception
    {
        [DataMember]
        public TFault Fault { get; private set; }

        public FaultException(TFault fault)
        {
            Fault = fault;
        }
        public FaultException(string message, TFault fault)
            : base(message)
        {
            Fault = fault;
        }
    }
}