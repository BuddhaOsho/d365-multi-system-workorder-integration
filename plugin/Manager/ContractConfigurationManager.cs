using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using Microsoft.Crm.Sdk.Messages;
using Sodexo.iFM.Shared.EntityController;
namespace Sodexo.iFM.Plugins.Manager
{
    public class ContractConfigurationManager : ManagerBase
    {
        public ContractConfigurationManager(ILocalPluginContext localPluginContext)
        : base(localPluginContext)
        {

        }
        //Ravi Sonal: See the placement of this code
        public static EntityCollection getRecordsByLanguages(EntityReference contract, List<LanguageRecord> languages, OptionSetValue eventCode, IOrganizationService orgService)
        {
            string[] columnsToFetch = new string[] {
                "ifm_contractid",
                "ifm_languageid",
                "ifm_eventcode",
                "ifm_contractid",
                "ifm_isdefaulttemplate",
                "ifm_emailtemplatereferenceid",
                "ifm_contractnotificationconfigurationid"
            };

            FilterExpression languageFliter = new FilterExpression()
            {
                FilterOperator = LogicalOperator.Or
            };
            foreach (LanguageRecord language in languages)
            {
                languageFliter.Conditions.Add(new ConditionExpression("ifm_languageid", ConditionOperator.Equal, language.Id));
            }

            FilterExpression contractFliter = new FilterExpression()
            {
                FilterOperator = LogicalOperator.And,
                Conditions =
                {
                    new ConditionExpression("ifm_contractid", ConditionOperator.Equal , contract.Id),
                    new ConditionExpression("ifm_eventcode", ConditionOperator.Equal , eventCode.Value),
                    new ConditionExpression("statecode", ConditionOperator.Equal , 0)

                }
            };

            QueryExpression query = new QueryExpression()
            {
                EntityName = ContractNotificationConfigurationsRecord.logicalName,
                ColumnSet = new ColumnSet(columnsToFetch)
            };
            query.Criteria.AddFilter(languageFliter);
            query.Criteria.AddFilter(contractFliter);

            EntityCollection configurationRecords = orgService.RetrieveMultiple(query);
            return configurationRecords;
        }

        public static EntityCollection getCNCRecordsBySite(Guid siteId, List<LanguageRecord> languages, OptionSetValue eventCode, IOrganizationService orgService)
        {
            
            string[] columnsToFetch = new string[] {
                "ifm_name",
                "ifm_contractid",
                "ifm_languageid",
                "ifm_eventcode",
                "ifm_contractid",
                "ifm_isdefaulttemplate",
                "ifm_emailtemplatereferenceid",
                "ifm_contractnotificationconfigurationid"
            };

            FilterExpression languageFliter = new FilterExpression()
            {
                FilterOperator = LogicalOperator.Or
            };
            foreach (LanguageRecord language in languages)
            {
                languageFliter.Conditions.Add(new ConditionExpression("ifm_languageid", ConditionOperator.Equal, language.Id));
            }

            FilterExpression contractFliter = new FilterExpression()
            {
                FilterOperator = LogicalOperator.And,
                Conditions =
                {
                    new ConditionExpression("statecode", ConditionOperator.Equal , 0),
                    new ConditionExpression("ifm_eventcode", ConditionOperator.Equal , eventCode.Value)
                }
            };

            LinkEntity linkEntity = new LinkEntity(ContractNotificationConfigurationsRecord.logicalName, "ifm_ifm_contractnotificationconfiguration_a", "ifm_contractnotificationconfigurationid", "ifm_contractnotificationconfigurationid", JoinOperator.Inner);
            linkEntity.LinkCriteria.AddCondition("accountid", ConditionOperator.Equal, siteId);
            
            QueryExpression query = new QueryExpression()
            {
                EntityName = ContractNotificationConfigurationsRecord.logicalName,
                ColumnSet = new ColumnSet(columnsToFetch)
            };
            query.Criteria.AddFilter(languageFliter);
            query.Criteria.AddFilter(contractFliter);
            query.LinkEntities.Add(linkEntity);

            EntityCollection configurationRecords = orgService.RetrieveMultiple(query);
            return configurationRecords;
        }
        public static ContractNotificationConfigurationsRecord getDefaultConfigurationForContract(EntityReference contract, OptionSetValue eventCode, IOrganizationService orgService)
        {
            //Ravi Sonal: See the placement of this code
            ContractNotificationConfigurationsRecord config = new ContractNotificationConfigurationsRecord(orgService);
            QueryExpression query = config.getBaseQuery();
            query.Criteria = new FilterExpression()
            {
                Conditions =
                {
                    new ConditionExpression("ifm_contractid", ConditionOperator.Equal , contract.Id),
                    new ConditionExpression("ifm_eventcode", ConditionOperator.Equal , eventCode.Value),
                    new ConditionExpression("ifm_isdefaulttemplate", ConditionOperator.Equal, true)
                }
            };
            EntityCollection queryResult = orgService.RetrieveMultiple(query);

            if (queryResult.Entities.Count == 1)
            {
                config.Record = queryResult.Entities[0];
            }
            else if (queryResult.Entities.Count > 1)
            {
                //Ravi Sonal: Add better Code here
                config.Record = queryResult.Entities[0];
                //Ravi Sonal: May Have to throw Error in future
                //throw new Exception("Duplicate Default Contract Configuration Found");
            }
            else if (queryResult.Entities.Count < 0)
            {
                //Ravi Sonal: Add better Code here
                throw new Exception("No Default Contract Configuration Found");
            }
            return config;
        }
    }
}
