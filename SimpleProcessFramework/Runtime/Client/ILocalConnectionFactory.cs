namespace Spfx.Runtime.Client
{
    public interface ILocalConnectionFactory
    {
        bool IsLoopback(ref ProcessEndpointAddress addr);
        IClientInterprocessConnection GetLoopbackConnection();
    }
}