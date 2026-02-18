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
    public class ContactRecord : EntityBaseClass
    {
        public const string logicalName = "contact";
        //Ravi Sonal: Use Column filter here to optimize it
        public string[] columns = new string[] {
                "ifm_languageid",
                "ifm_siteid"
            };
        private EntityReference _language = null;
        public EntityReference Language{
            get{
                if (_language == null && this.Record.Contains("ifm_languageid"))
                    _language = (EntityReference)this.Record["ifm_languageid"];
                return _language;
            }
            set {
                _language = value;
            }
        }
        private EntityReference _site = null;
        public EntityReference Site
        {
            get
            {
                if (_site == null && this.Record.Contains("ifm_siteid"))
                    _site = (EntityReference)this.Record["ifm_siteid"];
                return _site;
            }
            set
            {
                _site = value;
            }
        }

        public ContactRecord(Entity record, IOrganizationService service)
         : base(service)
        {
            base.Id = record.Id;
            base.Record = record;
            base.LogicalName = logicalName;
        }
        public ContactRecord(EntityReference record, IOrganizationService service)
        : base(service)
        {
            base.Id = record.Id;
            base.LogicalName = logicalName;
            base.Retrieve(columns);
        }
        public ContactRecord(Guid id, IOrganizationService service)
         : base(service)
        {
            base.LogicalName = logicalName;
            base.Id = id;
            base.Retrieve(columns);
        }
        public ContactRecord(IOrganizationService service)
         : base(service)
        {
            base.LogicalName = logicalName;
        }
        public ContactRecord(Guid id)
         : base(id)
        {
            base.Id = id;
        }

        public void getDefaultLanguage()
        {

        }
        public EntityReference ToEntityReference()
        {
            return new EntityReference()
            {
                Id = this.Id,
                LogicalName = this.LogicalName
            };
        }
    }
}
