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
    public class AccountRecord : EntityBaseClass
    {
        public const string logicalName = "account";
        //Ravi Sonal: Use Column filter here to optimize it
        public string[] columns = new string[] {
                "ifm_languageid"
            };
        private EntityReference _language = null;
        public EntityReference Language
        {
            get
            {
                if (_language == null && this.Record.Contains("ifm_languageid"))
                    _language = (EntityReference)this.Record["ifm_languageid"];
                return _language;
            }
            set
            {
                _language = value;
            }
        }
        public AccountRecord(Entity record, IOrganizationService service)
         : base(service)
        {
            base.Id = record.Id;
            base.Record = record;
            base.LogicalName = logicalName;
        }
        public AccountRecord(EntityReference record, IOrganizationService service)
        : base(service)
        {
            base.Id = record.Id;
            base.LogicalName = logicalName;
            base.Retrieve(columns);
        }
        public AccountRecord(Guid id, IOrganizationService service)
         : base(service)
        {
            base.LogicalName = logicalName;
            base.Id = id;
            this.Retrieve(columns);
        }
        public AccountRecord(IOrganizationService service)
         : base(service)
        {
            base.LogicalName = logicalName;
        }
        public AccountRecord(Guid id)
         : base(id)
        {
            base.Id = id;
        }

        public void getDefaultLanguage()
        {


        }
    }
}
