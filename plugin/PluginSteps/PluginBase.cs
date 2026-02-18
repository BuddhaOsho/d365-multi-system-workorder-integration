using Microsoft.Xrm.Sdk;
using System;
using System.ServiceModel;
using ABC.iFM.Shared.EntityController;
namespace ABC.iFM.Plugins
{
    /* 
     * Plugin development guide: https://docs.microsoft.com/powerapps/developer/common-data-service/plug-ins
     * Best practices and guidance: https://docs.microsoft.com/powerapps/developer/common-data-service/best-practices/business-logic/
     */
    public abstract class PluginBase : IPlugin
    {
        protected string PluginClassName { get; }

        internal PluginBase(Type pluginClassName)
        {
            PluginClassName = pluginClassName.ToString();
        }

        public void Execute(IServiceProvider serviceProvider)
        {
            if (serviceProvider == null)
            {
                throw new InvalidPluginExecutionException("serviceProvider");
            }

            var localPluginContext = new LocalPluginContext(serviceProvider);

            


            localPluginContext.Trace($"Entered {PluginClassName}.Execute() " +
                $"Correlation Id: {localPluginContext.PluginExecutionContext.CorrelationId}, " +
                $"Initiating User: {localPluginContext.PluginExecutionContext.InitiatingUserId}");

            try
            {
                ExecuteCdsPlugin(localPluginContext);

                // Now exit - if the derived plugin has incorrectly registered overlapping event registrations, guard against multiple executions.
                return;
            }
            catch (FaultException<OrganizationServiceFault> orgServiceFault)
            {
                localPluginContext.Trace($"Exception: {orgServiceFault.ToString()}");

                throw new InvalidPluginExecutionException($"OrganizationServiceFault: {orgServiceFault.Message}", orgServiceFault);
            }
            finally
            {
                localPluginContext.Trace($"Exiting {PluginClassName}.Execute()");
            }
        }

        protected virtual void ExecuteCdsPlugin(ILocalPluginContext localPluginContext)
        {
            // Do nothing. 
        }
    }

    /*
    * This interface provides an abstraction on top of IServiceProvider for commonly used PowerApps CDS Plugin development constructs
    */
    public interface ILocalPluginContext
    {
        // The PowerApps CDS organization service for current user account
        IOrganizationService CurrentUserService { get; }

        // The PowerApps CDS organization service for system user account
        IOrganizationService SystemUserService { get; }

        // IPluginExecutionContext contains information that describes the run-time environment in which the plugin executes, information related to the execution pipeline, and entity business information
        IPluginExecutionContext PluginExecutionContext { get; }

        // Synchronous registered plugins can post the execution context to the Microsoft Azure Service Bus.
        // It is through this notification service that synchronous plug-ins can send brokered messages to the Microsoft Azure Service Bus
        IServiceEndpointNotificationService NotificationService { get; }

        // Provides logging run time trace information for plug-ins. 
        ITracingService TracingService { get; }

        Entity TargetEntity { get; }

        Entity PostImage { get; }
        Entity PreImage { get; }

        // Writes a trace message to the CDS trace log
        void Trace(string message);
    }

    public class LocalPluginContext : ILocalPluginContext
    {
        private readonly string postImageAlias = "PostImage";
        private readonly string preImageAlias = "PreImage";
        internal IServiceProvider ServiceProvider { get; private set; }

        public IOrganizationService CurrentUserService { get; }

        public IOrganizationService SystemUserService { get; private set; }

        public IPluginExecutionContext PluginExecutionContext { get; }

        public IServiceEndpointNotificationService NotificationService { get; }

        public ITracingService TracingService { get; }

        internal Entity _targetEntity;
        public Entity TargetEntity
        {
            get
            {
                if (_targetEntity == null)
                {
                    _targetEntity = new Entity();
                    _targetEntity = PluginExecutionContext.InputParameters["Target"] as Entity;
                    if (_targetEntity == null)
                    {
                        TracingService.Trace("Target entity is null");
                    }
                }
                return _targetEntity;
            }
        }
        internal Entity _postImage;
        public Entity PostImage
        {
            get
            {
                if (_postImage == null)
                {
                    _postImage = new Entity();

                    if(PluginExecutionContext.PostEntityImages != null
                        && PluginExecutionContext.PostEntityImages.Contains(this.postImageAlias))
                    {
                        _postImage = PluginExecutionContext.PostEntityImages[this.postImageAlias];                        
                    }
                    if (_postImage == null)
                    {
                        TracingService.Trace("PostImage entity is null");
                    }
                }
                return _postImage;
            }
        }
        internal Entity _preImage;
        public Entity PreImage
        {
            get
            {
                if (_preImage == null)
                {
                    _preImage = new Entity();

                    if (PluginExecutionContext.PreEntityImages != null
                        && PluginExecutionContext.PreEntityImages.Contains(this.preImageAlias))
                    {
                        _preImage = PluginExecutionContext.PreEntityImages[this.preImageAlias];
                    }
                    if (_preImage == null)
                    {
                        TracingService.Trace("PreImage entity is null");
                    }
                }
                return _preImage;
            }
        }

        private LocalPluginContext()
        {
            //Prevents a default instance of the <see cref="LocalPluginContext"/> class from being created.
        }
        public LocalPluginContext(IServiceProvider serviceProvider)
        {
            if (serviceProvider == null)
            {
                throw new InvalidPluginExecutionException("serviceProvider");
            }

            PluginExecutionContext = (IPluginExecutionContext)serviceProvider.GetService(typeof(IPluginExecutionContext));

            TracingService = new LocalTracingService(serviceProvider);

            NotificationService = (IServiceEndpointNotificationService)serviceProvider.GetService(typeof(IServiceEndpointNotificationService));

            IOrganizationServiceFactory factory = (IOrganizationServiceFactory)serviceProvider.GetService(typeof(IOrganizationServiceFactory));

            CurrentUserService = factory.CreateOrganizationService(PluginExecutionContext.UserId);

            SystemUserService = factory.CreateOrganizationService(null);

            this.ServiceProvider = serviceProvider;

        }
        public void Trace(string message)
        {
            if (string.IsNullOrWhiteSpace(message) || TracingService == null)
            {
                return;
            }

            TracingService.Trace(message);
        }
        internal void SetServiceAccount()
        {
            //PriteshWankhade: See if we can use it properly         
            SystemSettingRecord systemSettings = new SystemSettingRecord(this.CurrentUserService);
            //PriteshW: Convert to to Static Method
            string serviceAccountName = systemSettings.getValueBySettingName("ifm_name");


            SystemUserRecord serviceAccountUser = new SystemUserRecord(this.CurrentUserService);
            serviceAccountUser.GetUserByFullName(serviceAccountName);
            //Ravi Sonal: Add error handling
            IOrganizationServiceFactory factory = (IOrganizationServiceFactory)this.ServiceProvider.GetService(typeof(IOrganizationServiceFactory));
            if (factory == null) throw new ArgumentNullException(nameof(factory));
            this.SystemUserService = factory.CreateOrganizationService(serviceAccountUser.Id);
        }
    }

    /*
     * Specialized ITracingService implementation that prefixes all traced messages with a time delta for Plugin performance diagnostics
     */
    public class LocalTracingService : ITracingService
    {
        private readonly ITracingService _tracingService;

        private DateTime _previousTraceTime;

        public LocalTracingService(IServiceProvider serviceProvider)
        {
            DateTime utcNow = DateTime.UtcNow;

            var context = (IExecutionContext)serviceProvider.GetService(typeof(IExecutionContext));

            DateTime initialTimestamp = context.OperationCreatedOn;

            if (initialTimestamp > utcNow)
            {
                initialTimestamp = utcNow;
            }

            _tracingService = (ITracingService)serviceProvider.GetService(typeof(ITracingService));

            _previousTraceTime = initialTimestamp;
        }

        public void Trace(string message, params object[] args)
        {
            var utcNow = DateTime.UtcNow;

            // The duration since the last trace.
            var deltaMilliseconds = utcNow.Subtract(_previousTraceTime).TotalMilliseconds;

            _tracingService.Trace($"[+{deltaMilliseconds:N0}ms)] - {message}");

            _previousTraceTime = utcNow;
        }
    }
}
