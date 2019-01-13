namespace SimpleProcessFramework.Runtime.Server
{
    public interface IClientConnectionManager
    {
        void AddListener(IConnectionListener listener);
        void RemoveListener(IConnectionListener listener);

        IInterprocessClientChannel GetClientChannel(long connectionId);
    }
}