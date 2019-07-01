#if NETCOREAPP
namespace Spfx.Tests
{
    /// <summary>
    /// https://github.com/nunit/nunit/issues/3282
    /// </summary>
    public class TimeoutAttribute : Attribute
    {
        public TimeoutAttribute(int defaultTestTimeout)
        {
        }
    }
}
#endif

