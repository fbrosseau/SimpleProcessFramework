﻿using Spfx.Diagnostics;
using Spfx.Reflection;
using Spfx.Runtime.Server;
using Spfx.Serialization;
using Spfx.Utilities;
using Spfx.Utilities.Threading;
using System;
using System.Threading.Tasks;

namespace Spfx.Tests.Integration
{
    public interface IExceptionReportingEndpoint
    {
        Task ReportException(IRemoteExceptionInfo remoteExceptionInfo);
    }

    public class ExceptionReportingEndpoint : AbstractProcessEndpoint, IExceptionReportingEndpoint
    {
        public const string EndpointId = "ExceptionReportingEndpoint";

        [ThreadStatic]
        private static ExceptionReportingEndpoint t_current;

        private readonly TaskCompletionSource<VoidType> m_failureTcs = new TaskCompletionSource<VoidType>();

        public static Task GetUnhandledExceptionTask()
        {
            return t_current?.m_failureTcs.Task;
        }

        public ExceptionReportingEndpoint()
        {
            t_current = this;
        }

        public Task ReportException(IRemoteExceptionInfo remoteExceptionInfo)
        {
            var ex = remoteExceptionInfo.RecreateException();
            m_failureTcs.TrySetException(ex);
            return Task.CompletedTask;
        }
    }

    internal class TestUnhandledExceptionsHandler : DefaultUnhandledExceptionHandler
    {
        private readonly IProcess m_localProcess;

        public TestUnhandledExceptionsHandler(ITypeResolver r)
            : base(r)
        {
            m_localProcess = r.GetSingleton<IProcess>(ignoreErrors: true);
        }

        public override void HandleCaughtException(Exception ex)
        {
            try
            {
                var addr = $"/{ProcessCore.MasterProcessUniqueId}/{ExceptionReportingEndpoint.EndpointId}";
                var ep = m_localProcess.ClusterProxy.CreateInterface<IExceptionReportingEndpoint>(addr);
                ep.ReportException(RemoteExceptionInfo.Create(ex, exposeRemoteCallstacks: true)).FireAndForget();
            }
            catch
            {
            }
        }
    }
}