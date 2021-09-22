using Spfx.Reflection;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo(DynamicCodeGenModule.DynamicModuleAssemblyIdentity)]

[assembly: SuppressMessage("Reliability", "CA2012:Use ValueTasks correctly", Justification = "", Scope = "Module")]
[assembly: SuppressMessage("Reliability", "CA2007:ConfigureAwait", Justification = "", Scope = "Module")]
[assembly: SuppressMessage("Reliability", "CA2008: Do not create tasks without passing a TaskScheduler", Justification = "", Scope = "Module")]