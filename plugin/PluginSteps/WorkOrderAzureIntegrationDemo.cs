using System;
using Microsoft.Xrm.Sdk;
using Sodexo.iFM.Plugins.Manager;
using Sodexo.iFM.Shared.EntityController;

namespace Sodexo.iFM.Plugins.PluginSteps.WorkOrder
{
    public class WorkOrderAzureIntegration : PluginBase
    {
        public WorkOrderAzureIntegration(string unsecureConfiguration, string secureConfiguration)
            : base(typeof(WorkOrderAzureIntegration))
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

            Entity EntityPostImage = localPluginContext.PostImage;

            Entity EntityPreImage = localPluginContext.PreImage;

            if (EntityPreImage == null)
            {
                throw new NullReferenceException($"{ nameof(EntityPreImage) } is null.");
            }
            if (EntityPostImage == null)
            {
                throw new NullReferenceException($"{ nameof(EntityPostImage) } is null.");
            }
                                 
            string TraceMessage = "Plugin:WorkOrderAzureIntegration|";
            if (EntityPostImage == null)
                TraceMessage += "Pre Image: Null|";           
            
            if (EntityPostImage == null)
                TraceMessage += "Post Image: Null|";

            if (Entitytarget.LogicalName != WorkOrderRecord.logicalName)
            {
                TraceMessage += "|EntityName: " + Entitytarget.LogicalName + "|";
                localPluginContext.Trace(TraceMessage);
                return;
            }
            #endregion PreChecks

            
            AzureIntegrationManager azureIntegrationManager = new AzureIntegrationManager(localPluginContext);
            localPluginContext.Trace(TraceMessage);
            try
            {
                if (EntityPostImage.Contains("ifm_originatedind365") == false) // No action if work order is not originated in D365.
                    return;
                if (Entitytarget.Contains("ifm_servicecategorylevel3id") || Entitytarget.Contains("ifm_servicecategorylevel4id"))
                    azureIntegrationManager.LoadRegardingRecord(EntityPostImage,Entitytarget, EntityPreImage);
            }
            catch (Exception ex)
            {
                localPluginContext.Trace(TraceMessage + "|" + ex.Message);
                throw new InvalidPluginExecutionException(ex.Message, ex);
            }
            
        }


    }
}
