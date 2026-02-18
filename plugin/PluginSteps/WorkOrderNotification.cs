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
    public class WorkOrderNotification : PluginBase
    {
        public WorkOrderNotification(string unsecureConfiguration, string secureConfiguration)
            : base(typeof(WorkOrderNotification))
        {

        }
        protected override void ExecuteCdsPlugin(ILocalPluginContext localPluginContext)
        {
            #region PreChecks
            if (localPluginContext == null)
            {
                throw new ArgumentNullException("localContext");
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
            Entity EntityPostImage = localPluginContext.PostImage;
            if (EntityPostImage == null)
            {
                throw new NullReferenceException($"{ nameof(EntityPostImage) } is null.");
            }

            var Entitytarget = localPluginContext.TargetEntity;
            if (Entitytarget == null)
            {
                throw new NullReferenceException($"{ nameof(Entitytarget) } is null.");
            }
            #endregion PreChecks

            string TraceMessage = "Plugin:WorkOrderNotification & Depth" + localPluginContext.PluginExecutionContext.Depth + "|";

            if (localPluginContext.PluginExecutionContext.Depth > 1)
            {
                TraceMessage += "|Depth reached more than 1|";
                localPluginContext.Trace(TraceMessage);
                return;
            }

            WorkOrderManager manager = new WorkOrderManager(localPluginContext);
            try
            {
                TraceMessage += "|Regarding WO: " + EntityPreImage.Id + "|";
                manager.runAsyncWONotification(EntityPreImage, EntityPostImage, Entitytarget);
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
