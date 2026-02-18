using Microsoft.Xrm.Sdk;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Sodexo.iFM.Shared.EntityController;
using Microsoft.Xrm.Sdk.Query;
using Microsoft.Crm.Sdk.Messages;

namespace Sodexo.iFM.Plugins.Manager
{
    public class ContactManager :ManagerBase
    {
        public IOrganizationService orgService;
        public IOrganizationService orgServiceAdmin;

        public ContactRecord PreImage;
        public ContactRecord PostImage;
        public ContactRecord TargetImage;

        public ContactManager(ILocalPluginContext localPluginContext)
           : base(localPluginContext)
        {
            orgService = this.LocalPluginContext.CurrentUserService;
            orgServiceAdmin = this.LocalPluginContext.SystemUserService;
        }

        private void loadPreChecks(Entity preImage, Entity postImage, Entity targetImage)
        {

            if (postImage != null)
                this.PostImage = new ContactRecord(postImage, this.LocalPluginContext.CurrentUserService);

            if (preImage != null)
                this.PreImage = new ContactRecord(preImage, this.LocalPluginContext.CurrentUserService);

            if (targetImage != null)
                this.TargetImage = new ContactRecord(targetImage, this.LocalPluginContext.CurrentUserService);
        }
        public void runAsyncShareContact(Entity preImage, Entity postImage, Entity targetImage)
        {

            loadPreChecks(preImage, postImage, targetImage);
            this.TraceMessage = "|Start Method: ContactManager.runAsyncShareContact|";

            if (TargetImage.Record.Contains("ifm_sitecontext") && TargetImage.Record.Attributes["ifm_sitecontext"] != null)
            {
                Guid siteContextId = TargetImage.Record.GetAttributeValue<EntityReference>("ifm_sitecontext").Id;
                Entity siteDetails = this.LocalPluginContext.SystemUserService.Retrieve("account", siteContextId, new ColumnSet("ownerid"));
                if (siteDetails != null && siteDetails.Contains("ownerid") && siteDetails.Attributes["ownerid"] != null)
                {
                    Guid siteOwnerId = siteDetails.GetAttributeValue<EntityReference>("ownerid").Id;
                    //Guid ContactOwnerId = TargetImage.Record.GetAttributeValue<EntityReference>("ownerid").Id;
                    //this.TraceMessage = "|SiteOwnerId : |" + siteOwnerId.ToString() + "|ContactOwnerId : |" + ContactOwnerId.ToString();

                    this.TraceMessage = "Share Contact Start";
                    ShareContactWithTeam(TargetImage.Record, siteOwnerId);
                    this.TraceMessage = "Share Contact End";

                }

            }
            this.TraceMessage += "|End Method: ContactManager.runAsyncShareContact|";
        }
        private void ShareContactWithTeam(Entity entity, Guid teamId)
        {

            var grantAccessRequest = new GrantAccessRequest
            {
                PrincipalAccess = new PrincipalAccess
                {
                    AccessMask = AccessRights.ReadAccess,
                    Principal = new EntityReference("team", teamId)
                },
                Target = new EntityReference(entity.LogicalName, entity.Id)
            };
            this.LocalPluginContext.SystemUserService.Execute(grantAccessRequest);

        }
    }
}
