using SimpleProcessFramework.Reflection;
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("SimpleProcessFramework.Tests")]
[assembly: InternalsVisibleTo("SimpleProcessFramework.TestApp")]
[assembly: InternalsVisibleTo(DynamicCodeGenModule.DynamicModuleName)]