using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Xrm.Sdk;
using Sodexo.iFM.Plugins.Manager;

namespace Sodexo.iFM.Plugins.PluginSteps.Contact
{
    public class PostContactUpdateAsync : PluginBase
    {
        public PostContactUpdateAsync(string unsecureConfiguration, string secureConfiguration)
           : base(typeof(PostContactUpdateAsync))
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
            #endregion PreChecks

            string TraceMessage = "Plugin:PostContactUpdateAsync & Depth - " + localPluginContext.PluginExecutionContext.Depth + "|";

            
            if (localPluginContext.PluginExecutionContext.Depth > 2)
            {
                TraceMessage += "|Depth reached more than 2|";
                localPluginContext.Trace(TraceMessage);
                return;
            }

            ContactManager manager = new ContactManager(localPluginContext);
            try
            {
                TraceMessage += "|Regarding Contact: " + Entitytarget.Id + "|";
                manager.runAsyncShareContact(null, null, Entitytarget);
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
