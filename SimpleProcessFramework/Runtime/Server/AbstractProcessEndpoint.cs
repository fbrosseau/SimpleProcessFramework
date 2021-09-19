using Spfx.Utilities.Threading;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Spfx.Runtime.Server
{
    public interface IProcessEndpoint : IAsyncDestroyable
    {
        ProcessEndpointAddress EndpointAddress { get; }
        Task InitializeAsync(IProcess parentProcess, ProcessEndpointAddress endpointAddress);

        bool FilterMessage(IInterprocessRequestContext request);
    }

    public class AbstractProcessEndpoint : AsyncDestroyable, IProcessEndpoint
    {
        protected IProcess ParentProcess { get; private set; }
        protected ProcessEndpointAddress EndpointAddress { get; private set; }

        ProcessEndpointAddress IProcessEndpoint.EndpointAddress => EndpointAddress;

        private CancellationTokenSource m_disposeTokenSource;

        protected CancellationToken GetDisposeToken()
        {
            if (m_disposeTokenSource != null)
                return m_disposeTokenSource.Token;

            if (HasTeardownStarted)
                return new CancellationToken(true);

            var cts = new CancellationTokenSource();
            lock (DisposeLock)
            {
                if (HasTeardownStarted)
                    return new CancellationToken(true);

                if (null != Interlocked.CompareExchange(ref m_disposeTokenSource, cts, null))
                    cts.Dispose();
            }

            return m_disposeTokenSource.Token;
        }

        protected override void OnDispose()
        {
            SignalDispose();

            (ParentProcess as IProcessInternal)?.OnEndpointDisposed(EndpointAddress.EndpointId);

            base.OnDispose();
        }

        protected override ValueTask OnTeardownAsync(CancellationToken ct = default)
        {
            SignalDispose();
            return base.OnTeardownAsync(ct);
        }

        private void SignalDispose()
        {
            var cts = m_disposeTokenSource;
            m_disposeTokenSource = null;

            cts?.SafeCancelAndDisposeAsync();
        }

        Task IProcessEndpoint.InitializeAsync(IProcess parentProcess, ProcessEndpointAddress endpointAddress)
        {
            ParentProcess = parentProcess;
            EndpointAddress = endpointAddress;
            return InitializeAsync().AsTask();
        }

        protected virtual ValueTask InitializeAsync()
        {
            return default;
        }

        protected virtual bool FilterMessage(IInterprocessRequestContext request) => true;

        bool IProcessEndpoint.FilterMessage(IInterprocessRequestContext request)
        {
            return FilterMessage(request);
        }
    }
}