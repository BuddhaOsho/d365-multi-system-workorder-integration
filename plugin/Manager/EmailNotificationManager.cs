using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Sodexo.iFM.Shared.EntityController;
using Microsoft.Crm.Sdk.Messages;
using Microsoft.Xrm.Sdk;

namespace Sodexo.iFM.Plugins.Manager
{
    public class EmailNotificationManager : ManagerBase
    {

        public IOrganizationService orgService;

        public EntityReference emailDefaultTemplate;
        public EntityReference From;
        public List<EntityReference> ToRecipient = new List<EntityReference>();
        public List<EntityReference> CCRecipient = new List<EntityReference>();
        public CurrencyRecord TransactionCurrency;
        public EntityReference Owner;
        public EntityReference Regarding;
        public OptionSetValue EventCode;
        public string TraceMessage;

        private ServiceRequestRecord regardingSR;
        private WorkOrderRecord regardingWO;
        private ClientContractRecord contract;
        private List<LanguageRecord> languagePriority;
        private ContractNotificationConfigurationsRecord EmailTemplateConfiguration;
        private ContractNotificationSendersRecord emailSender;
        private ContactRecord affectedContact;
        private AccountRecord affectedSite;

        public EmailNotificationManager(ILocalPluginContext localPluginContext)
            : base(localPluginContext)
        {
            orgService = this.LocalPluginContext.CurrentUserService;
        }
        public void LoadEvent()
        {

        }
        public void ClassifyEvent()
        {

        }

        public void LoadRegardingRecord(Entity record, OptionSetValue eventCodeOptions)
        {
            if (record.LogicalName == ServiceRequestRecord.logicalName)
            {
                this.regardingSR = new ServiceRequestRecord(record, this.orgService);
                this.EmailTemplateConfiguration = new ContractNotificationConfigurationsRecord(this.orgService);
                this.emailSender = new ContractNotificationSendersRecord(this.orgService);
                this.contract = new ClientContractRecord(this.orgService);

                SetServiceRequestContext(eventCodeOptions);
            }
            if (record.LogicalName == WorkOrderRecord.logicalName)
            {
                this.regardingWO = new WorkOrderRecord(record, this.orgService);
                this.EmailTemplateConfiguration = new ContractNotificationConfigurationsRecord(this.orgService);
                this.emailSender = new ContractNotificationSendersRecord(this.orgService);
                this.contract = new ClientContractRecord(this.orgService);

                SetWorkOrderContext(eventCodeOptions);
            }
        }
        private void SetServiceRequestContext(OptionSetValue eventCode)
        {
            this.TraceMessage = "Method: ServiceRequestNotifications.SetServiceRequestContext|";

            //Context 0: record = Post Image of Incident
            //record.Retrieve(true);

            try
            {
                //Context 1: Add Contract Details
                this.contract.Id = this.regardingSR.Contract.Id;
                this.contract.Retrieve(this.contract.columns);
                this.TraceMessage += "Contract: " + this.contract.Id.ToString() + "|";
                this.LocalPluginContext.Trace(this.TraceMessage);

               
                //Context 2: Event Code
                this.EventCode = eventCode;


                //Context 3: Affected Contact / Site
                if (this.regardingSR.AffectedUser.LogicalName == ContactRecord.logicalName)
                {
                    if (this.regardingSR.AffectedUser != null)
                    {
                        this.affectedContact = new ContactRecord(
                            this.regardingSR.AffectedUser,
                            this.orgService);

                        this.TraceMessage = "Affected Contact: " + this.affectedContact.Id.ToString() + "|";
                    }
                    if (affectedContact.Site != null)
                    {
                        this.affectedSite = new AccountRecord(
                            affectedContact.Site,
                            this.orgService);
                        this.TraceMessage += "Affected Site: " + this.affectedSite.Id.ToString() + "|";

                    }
                }
                else if (this.regardingSR.AffectedUser.LogicalName == AccountRecord.logicalName)
                {
                    this.affectedSite = new AccountRecord(
                        this.regardingSR.AffectedUser,
                        this.orgService);
                    this.TraceMessage += "Affected Site: " + this.affectedSite.Id.ToString() + "|";
                }
                this.LocalPluginContext.Trace(this.TraceMessage);

                //Context 4: Langugage Priority for site/ Contact
                getLanguagePriorityFromServiceRequest();
                this.LocalPluginContext.Trace(this.TraceMessage);

                //Context 5: Email Template
                //Contract, Event Code, language, Default
                getTemplateByLanguagePriority();
                this.LocalPluginContext.Trace(this.TraceMessage);

                if (this.emailDefaultTemplate == null)
                {
                    return;
                }

                //Context 6: Transaction Currency
                //Ravi Sonal: Add null cheks here
                this.TransactionCurrency = new CurrencyRecord(
                    this.regardingSR.TransactionCurrency,
                    this.orgService);
                this.TraceMessage += "Currency: " + this.TransactionCurrency.Id + "|";

                //Context 7:Owner Details
                this.Owner = this.regardingSR.Owner;
                this.TraceMessage += "Owner: " + Owner.Id + "|";

                //Context 8: Email Regardng ID
                this.Regarding = this.regardingSR.ToEntityReference();
                this.TraceMessage += "RegardingObject: " + this.Regarding.Id + "|";

                //Context 9: Sender/Receiver
                emailSender.Contract.Id = contract.Id;
                this.From = emailSender.SenderReference;
                this.TraceMessage += "FromParty: " + this.From.Id + "|";


                this.ToRecipient.Add(this.regardingSR.AffectedUser);
                this.TraceMessage += "ToPartyCount: " + this.ToRecipient.Count + "|";
                this.LocalPluginContext.Trace(this.TraceMessage);

                SendEmailFromTemplateResponse emailUsingTemplateResp = this.SendEmailUsingTemplate();

                //Create Log
                if (emailUsingTemplateResp == null)
                {
                    string subject = string.Format("Contract Notification - {0}", this.regardingSR.getTransactionNumber());
                    string description = "Failed to send email.";
                    CreateLog(subject, description, this.regardingSR);
                }


                //Ravi Sonal: Move this code somewhere
                //Ravi Sonal: Work on Message
                /*
                if (sender == null || (sender != null && sender.Count == 0))
                {
                    tracingService.Trace("Contract Notification Sender has not been setup yet.");
                    string lcid = UserSettingManager.GetUserLanguage(userId, service);
                    if (!string.IsNullOrEmpty(lcid))
                    {
                        MessageTranslationManager translationManager = new MessageTranslationManager(this.context);
                        throw new InvalidPluginExecutionException(translationManager.GetTranslation(service, tracingService, "GN002", lcid));
                    }
                }
                */
            }
            catch (Exception ex)
            {
                this.LocalPluginContext.Trace(TraceMessage);
                this.LocalPluginContext.Trace(ex.Message);
                throw new InvalidPluginExecutionException(ex.Message, ex);
            }
        }
        private void SetWorkOrderContext(OptionSetValue eventCode)
        {

            this.TraceMessage = "Method: ServiceRequestNotifications.SetServiceRequestContext|";
            if (this.regardingWO == null || this.regardingWO.Id == null)
                return;

            this.regardingSR = new ServiceRequestRecord(this.regardingWO.ServiceRequest.Id, this.orgService);

            //Context 0: record = Post Image of Incident
            //record.Retrieve(true);

            try
            {
                //Context 1: Add Contract Details
                this.contract.Id = regardingSR.Contract.Id;
                this.contract.Retrieve(this.contract.columns);
                this.TraceMessage += "Contract: " + this.contract.Id.ToString() + "|";
                this.LocalPluginContext.Trace(this.TraceMessage);

                //Context 2: Event Code
                this.EventCode = eventCode;


                //Context 3: Affected Contact / Site
                if (this.regardingSR.AffectedUser.LogicalName == ContactRecord.logicalName)
                {
                    if (this.regardingSR.AffectedUser != null)
                    {
                        this.affectedContact = new ContactRecord(
                            this.regardingSR.AffectedUser,
                            this.orgService);

                        this.TraceMessage = "Affected Contact: " + this.affectedContact.Id.ToString() + "|";
                    }
                    if (affectedContact.Site != null)
                    {
                        this.affectedSite = new AccountRecord(
                            affectedContact.Site,
                            this.orgService);
                        this.TraceMessage += "Affected Site: " + this.affectedSite.Id.ToString() + "|";

                    }
                }
                else if (this.regardingSR.AffectedUser.LogicalName == AccountRecord.logicalName)
                {
                    this.affectedSite = new AccountRecord(
                        this.regardingSR.AffectedUser,
                        this.orgService);
                    this.TraceMessage += "Affected Site: " + this.affectedSite.Id.ToString() + "|";
                }
                this.LocalPluginContext.Trace(this.TraceMessage);

                //Context 4: Langugage Priority for site/ Contact
                getLanguagePriorityFromServiceRequest();
                this.LocalPluginContext.Trace(this.TraceMessage);

                //Context 5: Email Template
                //Contract, Event Code, language, Default
                getTemplateByLanguagePriority();
                this.LocalPluginContext.Trace(this.TraceMessage);

                //Context 6: Transaction Currency
                //Ravi Sonal: Add null cheks here
                this.TransactionCurrency = new CurrencyRecord(
                    this.regardingSR.TransactionCurrency,
                    this.orgService);
                this.TraceMessage += "Currency: " + this.TransactionCurrency.Id + "|";


                //Context 7:Owner Details
                this.Owner = this.regardingSR.Owner;
                this.TraceMessage += "Owner: " + Owner.Id + "|";

                //Context 8: Email Regardng ID
                this.Regarding = this.regardingSR.ToEntityReference();
                this.TraceMessage += "RegardingObject: " + this.Regarding.Id + "|";

                //Context 9: Sender/Receiver
                emailSender.Contract.Id = contract.Id;
                this.From = emailSender.SenderReference;
                this.TraceMessage += "FromParty: " + this.From.Id + "|";

                SendEmailFromTemplateResponse emailUsingTemplateResp = this.SendEmailUsingTemplate();

                //Create Log
                if (emailUsingTemplateResp == null)
                {
                    string subject = string.Format("Contract Notification - {0}", this.regardingSR.getTransactionNumber());
                    string description = "Failed to send email.";
                    CreateLog(subject, description, this.regardingSR);
                }
            }
            catch (Exception ex)
            {
                this.LocalPluginContext.Trace(TraceMessage);
                this.LocalPluginContext.Trace(ex.Message);
                throw new InvalidPluginExecutionException(ex.Message, ex);
            }
        }
        private SendEmailFromTemplateResponse SendEmailUsingTemplate()
        {
            //Check Context 
            if (this.ToRecipient.Count < 1)
                return null;
            if (this.From == null || this.From.Id == null)
                return null;
            if (this.TransactionCurrency == null || this.TransactionCurrency.Id == null)
                return null;
            if (this.Regarding == null || this.Regarding.Id == null)
                return null;
            if (this.emailDefaultTemplate == null || this.emailDefaultTemplate.Id == null)
                return null;

            //Send Email Instance
            EmailRecord emailRecord = new EmailRecord(this.orgService);
            emailRecord.AddFromParty(this.From);
            emailRecord.AddToParty(this.ToRecipient);
            emailRecord.Owner = this.Owner;
            emailRecord.TransactionCurrency = this.TransactionCurrency.ToEntityReference();

            SendEmailFromTemplateResponse emailUsingTemplateResp = emailRecord.SendEmailUsingTemplate(this.emailDefaultTemplate.Id, this.Regarding);
            return emailUsingTemplateResp;
        }
        private void getLanguagePriorityFromServiceRequest()
        {

            /*
             * if(contactid)
             * {
             * langugage 1: Contact -->ifm_languageid 
             *          see if template available for this langugage
             * langugage 2: Contact --> Site ifm_siteid --> ifm_languageid 
             *          see if template available for this langugage
             * }
             * else if(accountid)
             * {
             * langugage 3: Affected user is account 
             *      Acount --> ifm_languageid
             *          see if template available for this langugage
             * }
             * */

            languagePriority = new List<LanguageRecord>();
            this.TraceMessage = "languagePriority|";
            if (this.affectedContact != null)
            {
                if (this.affectedContact.Language != null)
                {
                    this.languagePriority.Add(new LanguageRecord(
                        this.affectedContact.Language,
                        this.orgService));
                }
                if (this.affectedSite.Language != null)
                {
                    this.languagePriority.Add(new LanguageRecord(
                        this.affectedSite.Language,
                        this.orgService));
                }
            }
            else if (this.affectedSite != null)
            {
                if (this.affectedSite.Language != null)
                {
                    this.languagePriority.Add(new LanguageRecord(
                        this.affectedSite.Language,
                        this.orgService));
                }
            }
            //Ravi Sonal: Dont add duplicate entry. Put a check
            //Commenting Code as we have default Contract Configuration
            //Add default Enaglish Language
            //LanguageRecord englishlanguage = new LanguageRecord(this.orgService);
            //englishlanguage.getLanguageByLCID("1033");
            //this.languagePriority.Add(englishlanguage);

            foreach (LanguageRecord record in languagePriority)
            {
                this.TraceMessage += record.Record["ifm_languagecode"].ToString() + "|";
            }
        }
        private void getTemplateByLanguagePriority()
        {
            //Ravi Sonal: Place this code in proper format
            if (this.contract == null)
                throw new Exception("No Conract Defined");
            if (this.EventCode == null)
                throw new Exception("No EventCode Defined");
            if (this.languagePriority == null)
                throw new Exception("No langugage Defined");
            //if (this.languagePriority.Count < 1)
            //    throw new Exception("No langugage Defined");
            this.TraceMessage = "getTemplateByLanguagePriority:";

            EntityCollection configurationRecords = null;
            EntityCollection configurationRecordsBasedonSite = null;

            if (this.languagePriority.Count > 0)
            {
                if (this.regardingSR.Record.Contains("ifm_siteid"))
                {
                    Guid siteId = this.regardingSR.Record.GetAttributeValue<EntityReference>("ifm_siteid").Id;
                    this.TraceMessage += "Site Id " + siteId.ToString() + "|";
                    configurationRecordsBasedonSite = ContractConfigurationManager.getCNCRecordsBySite(siteId, languagePriority, this.EventCode, this.orgService);
                    if (configurationRecordsBasedonSite != null && configurationRecordsBasedonSite.Entities.Count > 0)
                    {
                        this.TraceMessage += " CNC - Site Record Count - " + configurationRecordsBasedonSite.Entities.Count + "|"; ;
                    }
                }
                               
                configurationRecords = ContractConfigurationManager.getRecordsByLanguages(
                this.contract.ToEntityReference(),
                this.languagePriority,
                this.EventCode,
                this.orgService);
                this.TraceMessage += "CNC - Contract Record Count - " +configurationRecords.Entities.Count + "|";
            }
            foreach (LanguageRecord language in this.languagePriority)
            {
                this.TraceMessage += "Langugage to Print: " + language.Id + "|";
            }
            if ((configurationRecords == null || configurationRecords.Entities.Count < 1) && (configurationRecordsBasedonSite == null || configurationRecordsBasedonSite.Entities.Count < 1))
            {
                this.TraceMessage += "No User/Site preferred Configuration found for Contract or Site.";
                this.TraceMessage += "Search Default";
                ContractNotificationConfigurationsRecord defaultConfig
                    = ContractConfigurationManager.getDefaultConfigurationForContract(this.contract.ToEntityReference(), this.EventCode, this.orgService);

                if (defaultConfig.Record == null)
                {
                    return;
                }

                defaultConfig.setEmailTemplate();
                this.emailDefaultTemplate = defaultConfig.EmailTemplate;

                this.TraceMessage += "EmailTemplate: " + this.emailDefaultTemplate.Id + "|";
                return;
            }



            if (configurationRecords.Entities.Count < 1 && (configurationRecordsBasedonSite == null || configurationRecordsBasedonSite.Entities.Count < 1))
            {
                this.TraceMessage += "No Configuration for Contract";
                return;
            }
            //bool isMatch = false;
           
            foreach (LanguageRecord language in this.languagePriority)
            {
                foreach (Entity configuration in configurationRecordsBasedonSite.Entities)
                {
                    ContractNotificationConfigurationsRecord ContractConfigRecord = new ContractNotificationConfigurationsRecord(
                        configuration,
                        this.orgService
                        );

                    if (ContractConfigRecord.Language.Id == language.Id)
                    {                        
                        this.TraceMessage += "Langugage: " + language.Id + "|";
                        ContractConfigRecord.setEmailTemplate();

                        this.emailDefaultTemplate = ContractConfigRecord.EmailTemplate;
                        this.TraceMessage += "EmailTemplate: " + this.emailDefaultTemplate.Id + "|";
                        return;
                    }
                }
            }
            foreach (LanguageRecord language in this.languagePriority)
            {
                foreach (Entity configuration in configurationRecords.Entities)
                {
                    ContractNotificationConfigurationsRecord ContractConfigRecord = new ContractNotificationConfigurationsRecord(
                        configuration,
                        this.orgService
                        );

                    if (ContractConfigRecord.Language.Id == language.Id)
                    {
                        //isMatch = true;
                        //this.TraceMessage += "Match: " + isMatch + "|";
                        this.TraceMessage += "Langugage: " + language.Id + "|";
                        ContractConfigRecord.setEmailTemplate();

                        this.emailDefaultTemplate = ContractConfigRecord.EmailTemplate;
                        this.TraceMessage += "EmailTemplate: " + this.emailDefaultTemplate.Id + "|";
                        return;
                    }
                }
            }
                
                //if (isMatch == true)
                //    break;
            
            this.TraceMessage += "No Email Template for avilable Langugage Priority";
            this.LocalPluginContext.Trace(this.TraceMessage);
        }
        private void CreateLog(string subject, string description, ServiceRequestRecord Sr)
        {
            LogRecord log = new LogRecord(this.orgService);
            log.Subject = subject;
            log.Description = description;
            log.IsActionRequired = true;
            log.HasResolved = false;
            log.Regarding = new EntityReference(Sr.LogicalName, Sr.Id);
            log.Create();
        }
    }
}
