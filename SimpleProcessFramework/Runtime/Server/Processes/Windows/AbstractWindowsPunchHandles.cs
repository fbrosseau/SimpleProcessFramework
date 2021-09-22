#if WINDOWS_BUILD

using Spfx.Utilities;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace Spfx.Runtime.Server.Processes.Windows
{
    internal abstract class AbstractWindowsPunchHandles : AbstractProcessInitializer
    {
        private static readonly SafeHandle s_inheritableHandleToThisProcess = Win32Interop.DuplicateLocalProcessHandle(
                Win32Interop.ThisProcessPseudoHandleValue,
                inheritable: true,
                HandleAccessRights.Synchronize);

        protected static readonly string StringHandleToThisProcess = SafeHandleUtilities.SerializeHandle(s_inheritableHandleToThisProcess);
        private static readonly SafeHandleToInherit[] s_extraHandlesToInherit = new[] { new SafeHandleToInherit(s_inheritableHandleToThisProcess, false) };

        public override IEnumerable<SafeHandleToInherit> ExtraHandlesToInherit => s_extraHandlesToInherit;
    }
}

#endif