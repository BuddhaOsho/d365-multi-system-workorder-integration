using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sodexo.iFM.Plugins.Controller
{   
    public class ContractNotificationConfigurationController : ControllerBase
    {
        public const string NOTIFICATION_TYPE = "NOTIFICATION_TYPE";
        public const string DEFAULT_QUEUE_SENDER = "Roth Command Center";
        public const string SETTING_EMAIL_SENDER = "Email Sender - Name";
        public const string ISO_CURRENCY = "USD";
        public ContractNotificationConfigurationController(ILocalPluginContext context)
            : base(context)
        {
            

        }

    }
}
