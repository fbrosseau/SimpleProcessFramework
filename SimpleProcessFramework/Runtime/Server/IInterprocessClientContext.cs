namespace SimpleProcessFramework.Runtime.Server
{
    public interface IInterprocessClientContext
    {
        IInterprocessClientChannel CallbackChannel { get; }
    }
}