#if WINDOWS_BUILD

using Spfx.Utilities;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace Spfx.Runtime.Server.Processes.Windows
{
    internal abstract class AbstractWindowsPunchHandles : AbstractProcessInitializer
    {
        private static SafeHandle s_inheritableHandleToThisProcess;
        protected static string StringHandleToThisProcess { get; }
        private static readonly IEnumerable<SafeHandleToInherit> s_extraHandlesToInherit;

        public override IEnumerable<SafeHandleToInherit> ExtraHandlesToInherit => s_extraHandlesToInherit;

        static AbstractWindowsPunchHandles()
        {
            s_inheritableHandleToThisProcess = Win32Interop.DuplicateLocalProcessHandle(
                Win32Interop.ThisProcessPseudoHandleValue,
                inheritable: true,
                HandleAccessRights.Synchronize);

            StringHandleToThisProcess = SafeHandleUtilities.SerializeHandle(s_inheritableHandleToThisProcess);
            s_extraHandlesToInherit = new[] { new SafeHandleToInherit(s_inheritableHandleToThisProcess, false) };
        }
    }
}

#endif