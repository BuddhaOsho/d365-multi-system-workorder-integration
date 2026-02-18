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
    public class PostWorkOrderIncidentTypeChange : PluginBase
    {


        public PostWorkOrderIncidentTypeChange(string unsecureConfiguration, string secureConfiguration)
            : base(typeof(PostWorkOrderIncidentTypeChange))
        {

        }
        protected override void ExecuteCdsPlugin(ILocalPluginContext localPluginContext)
        {
            //Ravi Sonal: Look for better structe for passing parameter
            //Ravi Sonal: Add proper checks for changes
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

            Entity EntityPostImage = localPluginContext.PostImage;

            Entity EntityPreImage = localPluginContext.PreImage;

            if (EntityPostImage == null)
            {
                throw new NullReferenceException($"{ nameof(EntityPostImage) } is null.");
            }
            #endregion PreChecks

            string TraceMessage = "Plugin:PostWorkOrderIncidentTypeChange & Depth - " + localPluginContext.PluginExecutionContext.Depth + "|";

            if (localPluginContext.PluginExecutionContext.Depth > 1)
            {
                TraceMessage += "|Depth reached more than 1|";
                localPluginContext.Trace(TraceMessage);
                return;
            }

            if (Entitytarget.Contains("ifm_servicecategorylevel4id")
                || Entitytarget.Contains("ifm_servicecategorylevel3id")
                || Entitytarget.Contains("ifm_servicecategorylevel2id"))
            {
                WorkOrderManager manager = new WorkOrderManager(localPluginContext);
                try
                {
                    TraceMessage += "|Regarding WO: " + EntityPreImage.Id + "|";
                    manager.runAsyncOnIncidentTypeChange(EntityPreImage, EntityPostImage, Entitytarget);
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
}
