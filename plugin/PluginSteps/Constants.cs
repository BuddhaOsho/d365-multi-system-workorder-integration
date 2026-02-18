using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sodexo.iFM.Plugins
{
    //Ravi SOnal: Set it properly
    public sealed class Constants
    {
        public sealed class PluginMessageNames
        {
            public const string Create = "Create";
            public const string Update = "Update";
        }

        public sealed class LanguageCodes
        {
            public const int English = 1033;
            public const int French = 1036;
        }

        public sealed class SecurityRoles
        {
            /// <summary>
            ///  Reference Data Admin
            /// </summary>
            public const string ReferenceDataAdmin = "IFM - Reference Data Admin";

            /// <summary>
            /// System Administator
            /// </summary>
            public const string SystemAdministrator = "System Administrator";
        }

        public sealed class NotificationTypes
        {
            public const string AddedToQueueEmail = "AddedToQueueEmail";
        }
    }
}
