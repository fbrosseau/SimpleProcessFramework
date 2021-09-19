namespace Spfx.Runtime.Server
{
    /// <summary>
    /// This special interface can be used to point to any remote endpoint address, whether it exists or not.
    /// It can then be used to generically subscribe to the loss of the endpoint
    /// </summary>
    public interface IAnyEndpoint
    {
    }
}