namespace Spfx.Runtime.Server
{
    public interface IInterprocessClientContext
    {
        IInterprocessClientChannel CallbackChannel { get; }
    }
}