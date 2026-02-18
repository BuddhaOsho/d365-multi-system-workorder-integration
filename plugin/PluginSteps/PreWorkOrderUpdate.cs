using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics.CodeAnalysis;
using System.ServiceModel;
using Microsoft.Crm.Sdk.Messages;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using Sodexo.iFM.Plugins.Manager;
using Sodexo.iFM.Shared.EntityController;


namespace Sodexo.iFM.Plugins.PluginSteps.WorkOrder
{
    public class PreWorkOrderUpdate : PluginBase
    {
        public PreWorkOrderUpdate(string unsecureConfiguration, string secureConfiguration)
            : base(typeof(PostWorkOrderUpdate))
        {

        }
        protected override void ExecuteCdsPlugin(ILocalPluginContext localPluginContext)
        {
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

            Entity EntityPreImage = localPluginContext.PreImage;
            if (EntityPreImage == null)
            {
                throw new NullReferenceException($"{ nameof(EntityPreImage) } is null.");
            }
            #endregion PreChecks
            string TraceMessage = "Plugin:PreWorkOrderUpdate & Depth" + localPluginContext.PluginExecutionContext.Depth + "|";

            if (localPluginContext.PluginExecutionContext.Depth > 2)
            {
                //Ravi Sonal: Why we seeing more than 1 depth trigger. we may need o control this.
                TraceMessage += "|Depth reached more than 2|";
                localPluginContext.Trace(TraceMessage);
                return;
            }

            WorkOrderManager manager = new WorkOrderManager(localPluginContext);
            try
            {
                TraceMessage += "|Regarding WO: " + EntityPreImage.Id + "|";
                manager.runPreUpdateSync(EntityPreImage, null, Entitytarget);
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
