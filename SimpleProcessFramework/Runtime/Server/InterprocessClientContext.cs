namespace Spfx.Runtime.Server
{
    internal class InterprocessClientContext : IInterprocessClientContext
    {
        public IInterprocessClientChannel CallbackChannel { get; }

        public InterprocessClientContext(IInterprocessClientChannel callbackChannel)
        {
            CallbackChannel = callbackChannel;
        }
    }
}