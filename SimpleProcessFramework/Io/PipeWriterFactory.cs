using Spfx.Utilities;
using System.IO;
using System.IO.Pipes;

namespace Spfx.Io
{
    internal static class PipeWriterFactory
    {
        internal static ILengthPrefixedStreamWriter CreateWriter(Stream stream, string name, bool? sync = null)
        {
            if (sync == null)
                sync = UseStreamSynchronously(stream);

            if (sync == true)
                return new SyncLengthPrefixedStreamWriter(stream, name);

            return new AsyncLengthPrefixedStreamWriter(stream);
        }

        internal static ILengthPrefixedStreamReader CreateReader(Stream stream, string name, bool? sync = null)
        {
            if (sync == null)
                sync = UseStreamSynchronously(stream);

            if (sync == true)
                return new SyncLengthPrefixedStreamReader(stream, name);

            return new AsyncLengthPrefixedStreamReader(stream);
        }

        private static bool UseStreamSynchronously(Stream stream)
        {
            if (!HostFeaturesHelper.IsWindows)
                return true;

            if (stream is PipeStream)
            {
                if (stream is AnonymousPipeServerStream || stream is AnonymousPipeClientStream)
                    return true;
            }

            return false;
        }
    }
}
