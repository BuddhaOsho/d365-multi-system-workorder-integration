using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sodexo.iFM.Plugins.Controller
{
    public abstract class ControllerBase
    {
        public ILocalPluginContext LocalPluginContext;

        public ControllerBase(ILocalPluginContext localPluginContext)
        {
            LocalPluginContext = localPluginContext;
        }
    }
}
