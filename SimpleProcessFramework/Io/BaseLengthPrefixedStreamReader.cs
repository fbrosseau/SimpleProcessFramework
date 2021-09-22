using Spfx.Utilities;
using Spfx.Utilities.Threading;
using System;
using System.IO;
using System.Threading.Tasks;

namespace Spfx.Io
{
    internal abstract class BaseLengthPrefixedStreamReader : Disposable, ILengthPrefixedStreamReader
    {
        protected Stream Stream { get; }
        protected int MaximumFrameSize { get; }
        private readonly AsyncQueue<StreamOrCode> m_readQueue;
        private readonly Task m_readLoop;

        protected byte[] SizeBuffer { get; } = new byte[4];

        public BaseLengthPrefixedStreamReader(Stream stream, int maximumFrameSize = int.MaxValue)
        {
            Guard.ArgumentNotNull(stream, nameof(stream));
            Stream = stream;
            MaximumFrameSize = maximumFrameSize;

            m_readQueue = new AsyncQueue<StreamOrCode>
            {
                DisposeIgnoredItems = true
            };

            m_readLoop = Task.Run(ReadLoop);
        }

        protected override void OnDispose()
        {
            m_readLoop.FireAndForget();
            Stream.Dispose();
            m_readQueue.Dispose();
        }

        public ValueTask<StreamOrCode> GetNextFrame()
        {
            return m_readQueue.DequeueAsync();
        }

        private async Task ReadLoop()
        {
            try
            {
                await InitializeAsync().ConfigureAwait(false);

                while (true)
                {
                    var frame = await ReceiveNextFrame().ConfigureAwait(false);
                    if (frame.IsEof)
                    {
                        m_readQueue.CompleteAdding().FireAndForget();
                    }
                    else
                    {
                        m_readQueue.Enqueue(frame);
                    }
                }
            }
            catch (Exception ex)
            {
                m_readQueue.Dispose(ex);
            }
        }

        protected virtual Task InitializeAsync()
        {
            return Task.CompletedTask;
        }

        internal abstract ValueTask<StreamOrCode> ReceiveNextFrame();
    }
}
