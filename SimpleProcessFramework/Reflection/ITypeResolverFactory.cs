namespace Spfx.Reflection
{
    public interface ITypeResolverFactory
    {
        ITypeResolver CreateRootResolver();
    }
}