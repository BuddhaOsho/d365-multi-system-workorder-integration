using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using Microsoft.Crm.Sdk.Messages;
namespace Sodexo.iFM.Shared.EntityController
{
    public class ClientContractRecord : EntityBaseClass
    {
        public const string logicalName = "ifm_contract";
        //Ravi Sonal: Change the columsn name
        public string[] columns = new string[] {
                "ifm_contractid",
                "ifm_contractname",
                "ifm_ssoportalurl",
                "ifmmob_mobileappthemeid",
                "ifm_ssoportalurl",
                "ifm_segmentid",
                "ifm_regioncode",
                "ifm_pricelistid",
                "owningbusinessunit",
                "ownerid",
                "ifm_languageid",
                "ifm_expirationdate",
                "ifm_contractcode",
                "ifm_contactid",
                "ifm_segment",
                "ifm_companyid",
                "ifm_commencementdate",
                "statuscode",
                "statecode",
            };
        private string _ifm_contractcode;
        public string ContractCode
        {
            get
            {
                if (_ifm_contractcode == null && this.Record.Contains("ifm_contractcode"))
                    _ifm_contractcode = (string)this.Record["ifm_contractcode"];
                return _ifm_contractcode;
            }
            set
            {
                this.Record["ifm_contractcode"] = value;
            }
        }
        public ClientContractRecord(Entity record, IOrganizationService service)
         : base(service)
        {
            base.Id = record.Id;
            base.Record = record;
            base.LogicalName = logicalName;
        }
        public ClientContractRecord(Guid id, IOrganizationService service)
         : base(service)
        {
            base.LogicalName = logicalName;
            base.Id = id;
            this.Retrieve(columns);
        }
        public ClientContractRecord(IOrganizationService service)
         : base(service)
        {
            base.LogicalName = logicalName;
        }
        public ClientContractRecord(Guid id)
         : base(id)
        {
            base.Id = id;
        }
        public EntityReference ToEntityReference()
        {
            return new EntityReference()
            {
                Id = this.Id,
                LogicalName = logicalName
            };
        }
    }
}
