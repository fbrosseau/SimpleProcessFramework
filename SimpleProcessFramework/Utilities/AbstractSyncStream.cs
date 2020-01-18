using Spfx.Utilities.Threading;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Spfx.Utilities
{
    /// <summary>
    /// Implements all possible overloads and redirects to the synchronous Span-based methods.
    /// Also declares all the modern span-based things as virtual (even if they don't exist in the current target framework)
    /// so that implementing classes can just override them without #IFs.
    /// </summary>
    internal abstract class AbstractSyncStream : Stream
    {
        private Task<int> m_latestIntTask;

        public override int Read(byte[] buffer, int offset, int count)
        {
            return Read(new Span<byte>(buffer, offset, count));
        }

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken ct)
        {
            Guard.ArgumentNotNull(buffer, nameof(buffer));
            if (ct.IsCancellationRequested)
                return Task.FromCanceled<int>(ct);
            return GetIntTask(Read(buffer, offset, count));
        }

        public override IAsyncResult BeginRead(byte[] buffer, int offset, int count, AsyncCallback callback, object state)
        {
            return TaskEx.ToApm(ReadAsync(buffer, offset, count), callback, state);
        }

        public override int EndRead(IAsyncResult asyncResult)
        {
            return TaskEx.EndApm<int>(asyncResult);
        }

        public virtual byte ReadByteOrThrow()
        {
            var b = ReadByte();
            if (b == -1)
                throw new EndOfStreamException();
            return (byte)b;
        }

#if NETCOREAPP || NETSTANDARD2_1_PLUS
        public abstract override int Read(Span<byte> buffer);
#else
        public abstract int Read(Span<byte> buffer);
#endif

#if NETCOREAPP || NETSTANDARD2_1_PLUS
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken ct = default)
#else
        public virtual ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken ct = default)
#endif
        {
            if (ct.IsCancellationRequested)
                return new ValueTask<int>(Task.FromCanceled<int>(ct));
            return GetIntValueTask(Read(buffer.Span));
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            Guard.ArgumentNotNull(buffer, nameof(buffer));
            Write(new Span<byte>(buffer, offset, count));
        }

        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken ct)
        {
            Guard.ArgumentNotNull(buffer, nameof(buffer));
            if (ct.IsCancellationRequested)
                return Task.FromCanceled<int>(ct);
            Write(new Span<byte>(buffer, offset, count));
            return Task.CompletedTask;
        }

        public override IAsyncResult BeginWrite(byte[] buffer, int offset, int count, AsyncCallback callback, object state)
        {
            return TaskEx.ToApm(TaskEx.ToVoidTypeTask(WriteAsync(buffer, offset, count)), callback, state);
        }

        public override void EndWrite(IAsyncResult asyncResult)
        {
            TaskEx.EndApm<VoidType>(asyncResult);
        }

        public override Task FlushAsync(CancellationToken ct)
        {
            if (ct.IsCancellationRequested)
                return Task.FromCanceled(ct);
            Flush();
            return Task.CompletedTask;
        }

#if NETCOREAPP || NETSTANDARD2_1_PLUS
        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken ct = default)
#else
        public ValueTask WriteAsync(Memory<byte> buffer, CancellationToken ct = default)
#endif
        {
            if (ct.IsCancellationRequested)
                return new ValueTask(Task.FromCanceled(ct));
            Write(buffer.Span);
            return default;
        }

#if NETCOREAPP || NETSTANDARD2_1_PLUS
        public abstract override void Write(ReadOnlySpan<byte> buffer);
#else
        public abstract void Write(ReadOnlySpan<byte> buffer);
#endif

        private Task<int> GetIntTask(int value, bool needValidTask = true)
        {
            var latest = m_latestIntTask;
            if (latest?.Result == value)
                return latest;

            if (m_latestIntTask is null || needValidTask)
            {
                m_latestIntTask = latest = TaskCache.FromResult(value);
                return latest;
            }

            return null;
        }

        private ValueTask<int> GetIntValueTask(int v)
        {
            var task = GetIntTask(v, needValidTask: false);
            if (task is null)
                return new ValueTask<int>(v);
            return new ValueTask<int>(task);
        }

#if NETCOREAPP || NETSTANDARD2_1_PLUS
        public abstract override void CopyTo(Stream destination, int bufferSize);
#else
        public new abstract void CopyTo(Stream destination, int bufferSize);
#endif
    }
}