using System.Collections.Generic;

namespace Spfx.Runtime.Server.Processes
{
    internal abstract class WindowsProcessStartupParameters : GenericProcessStartupParameters
    {

    }

    internal class GenericNetcoreProcessStartupParameters : GenericProcessStartupParameters
    {
        protected override void CreateFinalArguments(List<string> processArguments)
        {
            base.CreateFinalArguments(processArguments);
            CreateNetcoreArguments(processArguments);
        }
    }

    internal class NetcoreProcessStartupParameters : WindowsProcessStartupParameters
    {
        protected override void CreateFinalArguments(List<string> processArguments)
        {
            base.CreateFinalArguments(processArguments);
            CreateNetcoreArguments(processArguments);
        }
    }
}
