using SimpleProcessFramework.Reflection;
using SimpleProcessFramework.Runtime.Server;

namespace SimpleProcessFramework
{
    public class ProcessClusterConfiguration
    {
        private ITypeResolver m_typeResolver = ProcessCluster.DefaultTypeResolver;

        internal static ProcessClusterConfiguration Default { get; } = new ProcessClusterConfiguration();

        public ITypeResolver TypeResolver
        {
            get => m_typeResolver;
            set { m_typeResolver = value ?? ProcessCluster.DefaultTypeResolver; }
        }
    }
}
