using Spfx.Reflection;
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("Spfx.Tests")]
[assembly: InternalsVisibleTo("Spfx.TestApp")]
[assembly: InternalsVisibleTo(DynamicCodeGenModule.DynamicModuleName)]