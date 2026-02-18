using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Microsoft.Crm.Sdk.Messages;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using Sodexo.iFM.Plugins.Manager;
using Sodexo.iFM.Shared.EntityController;

namespace Sodexo.iFM.Plugins.PluginSteps.WorkOrder
{
    public class PostWorkOrderOwnerChange : PluginBase
    {


        public PostWorkOrderOwnerChange(string unsecureConfiguration, string secureConfiguration)
            : base(typeof(PostWorkOrderOwnerChange))
        {

        }
        protected override void ExecuteCdsPlugin(ILocalPluginContext localPluginContext)
        {
            //Ravi Sonal: Look for better structe for passing parameter
            #region PreChecks
            if (localPluginContext == null)
            {
                throw new ArgumentNullException("localContext");
            }

            var Entitytarget = localPluginContext.TargetEntity;
            if (Entitytarget == null)
            {
                throw new NullReferenceException($"{ nameof(Entitytarget) } is null.");
            }

            if (localPluginContext.CurrentUserService == null)
            {
                throw new NullReferenceException("Organization service is null.");
            }
            #endregion PreChecks

            string TraceMessage = "Plugin:PostWorkOrderOwnerChange & Depth - " + localPluginContext.PluginExecutionContext.Depth + "|";

            if (localPluginContext.PluginExecutionContext.Depth > 3)
            {
                TraceMessage += "|Depth reached more than 2|";
                localPluginContext.Trace(TraceMessage);
                return;
            }

            WorkOrderManager manager = new WorkOrderManager(localPluginContext);
            try
            {
                TraceMessage += "|Regarding WO: " + Entitytarget.Id + "|";
                manager.runAsyncOnOwnerChange(null, null, Entitytarget);
                localPluginContext.Trace(TraceMessage + manager.TraceMessage);
            }
            catch (Exception ex)
            {
                localPluginContext.Trace(TraceMessage + manager.TraceMessage + ex.Message);
                throw new InvalidPluginExecutionException(ex.Message, ex);
            }
        }
    }
}
