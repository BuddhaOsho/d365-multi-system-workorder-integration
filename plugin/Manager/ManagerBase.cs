using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sodexo.iFM.Plugins.Manager
{
    public abstract class ManagerBase
    {
        public ILocalPluginContext LocalPluginContext;
        public string TraceMessage;

        public ManagerBase(ILocalPluginContext localPluginContext)
        {
            this.LocalPluginContext = localPluginContext;
        }
    }
}
