namespace SimpleProcessFramework.Runtime.Server
{
    internal interface IProcessEndpointHandler
    {
        void HandleMessage(IInterprocessRequestContext req);
    }
}