using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
namespace Sodexo.iFM.Plugins.Controller
{
    using Manager;

    public class UserManagementController : ControllerBase
    {
        public const string BUProfileLogicalName = "ifm_businessunitprofiles";

        //public Entity BusinessUnitProfile;

        private Entity bu;

        public Entity BusinessUnit
        {
            get
            {
                return bu;
            }
            set
            {
                bu = value;
                //BusinessUnitProfile["ifm_businessunitguid"] = value.Id.ToString();
                //BusinessUnitProfile["ifm_name"] = value.Attributes["name"];
            }
        }
        public UserManagementController(ILocalPluginContext context)
            : base(context)
        {
            setEntityContext("Target");

        }

        public void syncBusinessunit(string Message, string Source)
        {
            if (Message == "Create" && Source == "businessunit")
            {
                this.createBUProfile();
            }
            if (Message == "Delete" && Source == "businessunit")
            {
                this.deleteBUProfile();
            }

        }
        private void setEntityContext(string objName)
        {
            Entity entity = null;
            if (objName == "Target")
            {
                //&& context.PluginExecutionContext.InputParameters.Contains("Target")
                //&& context.PluginExecutionContext.InputParameters["Target"] is Entity

                entity = (Entity)this.LocalPluginContext.PluginExecutionContext.InputParameters["Target"];

                // Check for entity name on which this plugin would be registered
                if (entity.LogicalName == "businessunit")
                {
                    this.BusinessUnit = entity;

                }
            }
            else if (objName == "Target")
            {

            }

        }
        private void createBUProfile()
        {
            setEntityContext("Target");

            //BusinessUnitManager businessUnitManager = new BusinessUnitManager(this.pluginContext.SystemUserService);
            //this.BU = businessUnit;
            Entity businessUnitProfile = new Entity("ifm_businessunitprofiles");
            businessUnitProfile["ifm_businessunitguid"] = this.BusinessUnit.Id.ToString();
            businessUnitProfile["ifm_name"] = this.BusinessUnit.Attributes["name"];
            //this.BUProfile = businessUnitProfile;
            //return businessUnitProfile;
            this.LocalPluginContext.CurrentUserService.Create(businessUnitProfile);
        }
        private void deleteBUProfile()
        {
            QueryExpression BUProfileQuery = new QueryExpression()
            {
                EntityName = "ifm_businessunitprofiles",
                ColumnSet = new ColumnSet("ifm_businessunitprofilesid"),
                Criteria = new FilterExpression()
                {
                    Conditions =
                    {
                        new ConditionExpression("ifm_businessunitguid", ConditionOperator.Equal, this.BusinessUnit.Id.ToString() )
                    }
                }

            };
            EntityCollection BUProfileCollections = this.LocalPluginContext.SystemUserService.RetrieveMultiple(BUProfileQuery);



            //businessUnit_preImage.Attributes["businessunitid"].ToString()
            if (BUProfileCollections.Entities.Count > 0)
            {
                this.LocalPluginContext.SystemUserService.Delete(BUProfileCollections[0].LogicalName, BUProfileCollections[0].Id);
            }
        }
        private void fetchBUProfile(string BUGuid)
        {
            


        }
    }
}
