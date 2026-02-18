using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


using Microsoft.Crm.Sdk.Messages;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Query;

using System.ServiceModel;
using System.Diagnostics.CodeAnalysis;

namespace Sodexo.iFM.Plugins.Manager
{
    public class ServiceRequestManager : ManagerBase
    {
        public IOrganizationService orgService;


        //public const string ACTION = "Action";
        //public const string QUALIFY = "Qualify";
        //private const string NO_QUEUE_FOUND = "Queue is not defined for service category";
        //private const string NO_PARENT_FOUND = "Queue is not defined for service category";
        //public const string RECLASSIFY_REROUTESR = "RerouteSR";
        //public const string RECLASSIFY_CANCELWO = "CancelExternalWO";
        //public const string RECLASSIFY_SOFTTOHARD = "SoftToHard";
        //public const string RECLASSIFY_SOFTTOSOFT = "SoftToSoft";
        //public const string TRANSITION_TYPE = "TRANSITION_TYPE";
        //public const string SETTING_TRANSITIVE = "Transitive Service Category - Code";
        //const string SUBSTATUS_PENDING_PARTS = "PPARTS";
        //const string SUBSTATUS_PENDING_VENDOR_SCHEDULING = "PVSCH";
        ////public const string ON_HOLD_REASON = "On Hold Reason - Message";
        //public const string WOTYPE_QUOTE = "QUOTE";
        //private const string PORTAL_USER = "SYSTEM";
        //private const string CONTRACT_HAS_ONHOLD = "Contract Has On Hold - Code";
        //private const int REQUESTTYPE_QUOTATION = 224960000;

        public ServiceRequestManager(ILocalPluginContext localPluginContext)
            : base(localPluginContext)
        {
            orgService = this.LocalPluginContext.CurrentUserService;
        }
        /*

        public string AssignToQueue(
            EntityReference serviceRequestRef,
            EntityReference serviceCategoryRef,
            string processStageName,
            Guid userId,
            out bool isAssigneeCurrentUser,
            out EntityReference outputQueueRef)
        {
            isAssigneeCurrentUser = false;


            string userMessage = string.Empty;
            string thirdPartyMessage = string.Empty;
            EntityReference thirdparty_Vendor = null;

            var serviceCategory = context.ifm_servicecategorySet.Single(r => r.Id == serviceCategoryRef.Id);

            outputQueueRef = GetOutputQueue(serviceCategoryRef, processStageName);
            var outputQueue = outputQueueRef != null ? outputQueueRef.Name : NO_QUEUE_FOUND;
            if (outputQueueRef == null)
                userMessage = NO_QUEUE_FOUND;

            //Ravi Sonal: move it to better place
            string lcid = UserSettingManager.GetUserLanguage(userId, organizationService);
            MessageTranslationManager translationManager = new MessageTranslationManager(this.context);

            //Ravi Sonal: WHat is this field "ifm_isworkorderrequired"
            //"IsWoRequired is {0}", serviceCategory.ifm_isworkorderrequired);



            if (outputQueue == NO_QUEUE_FOUND)
            {
                return userMessage;
            }

            //"Get Current SR from Context.");
            var latestSvcReq = context.IncidentSet.Single(r => r.Id == serviceRequestRef.Id);
            if (latestSvcReq == null)
            {
                throw new InvalidPluginExecutionException("Cannot get the current SR.");
            }

            var isWOCreate = latestSvcReq.ifm_isworkordercreate ?? false; //"WO Create: {0}", isWOCreate));

            // Update "WO Create" field
            bool isCreateWOCheck = false;
            if (processStageName == ACTION && serviceCategory?.ifm_isworkorderrequired == true)
            {
                if (!isWOCreate)
                {
                    bool allowRun = true;
                    int counter = 0, MAX_RETRY = 1;
                    // No Retry: any time an error related to a data operation occurs within a synchronous plug-in 
                    // the transaction for the entire operation will be ended.

                    Incident updatedSvcReq = new Incident()
                    {
                        LogicalName = Incident.EntityLogicalName,
                        Id = latestSvcReq.Id,
                        RowVersion = latestSvcReq.RowVersion
                    };

                    do
                    {
                        try
                        {
                            updatedSvcReq.ifm_isworkordercreate = true; //"Set WO Create to YES.");

                            UpdateRequest svcReqUpdate = new UpdateRequest()
                            {
                                Target = updatedSvcReq,
                                ConcurrencyBehavior = ConcurrencyBehavior.IfRowVersionMatches
                            };
                            counter++;
                            //"Update WO Create field - #{0}. Row Version: {1}.", counter, updatedSvcReq.RowVersion));
                            context.Execute(svcReqUpdate);
                            allowRun = false;
                            isCreateWOCheck = true;
                        }
                        catch (FaultException<OrganizationServiceFault> ex1)
                        {
                            tracingService.Trace("Create Log - Dirty record");
                            CreateLogWhenCreatingWO(organizationService, ex1.Message, latestSvcReq.TicketNumber, latestSvcReq.Id);
                            if (counter >= MAX_RETRY)
                            {
                                allowRun = false;
                            }
                            else
                            {
                                tracingService.Trace("Get Current SR from DB.");
                                updatedSvcReq = organizationService.Retrieve(Incident.EntityLogicalName,
                                        serviceRequestRef.Id, new ColumnSet("ifm_isworkordercreate"))
                                    .ToEntity<Incident>();
                            }
                        }
                        catch (Exception ex2)
                        {
                            allowRun = false;
                            CreateLogWhenCreatingWO(organizationService, ex2.Message, latestSvcReq.TicketNumber, latestSvcReq.Id);

                            tracingService.Trace("Failed to create Work Order. Unhandled exception.");
                            if (!string.IsNullOrEmpty(lcid))
                            {
                                // "Failed to create Work Order. Please try again.
                                throw new InvalidPluginExecutionException(translationManager.GetTranslation(organizationService, tracingService, "SR009", lcid));
                            }
                        }
                    } while (allowRun);
                }
            }

            // create queueitem
            Guid queueItemId = CreateQueueItem(serviceRequestRef, outputQueueRef);

            //"isCreateWOCheck: " + isCreateWOCheck);

            // create workorder 
            if (processStageName == ACTION && serviceCategory?.ifm_isworkorderrequired == true && isCreateWOCheck)
            {
                tracingService.Trace("Creating WorkOrder");
                this.CreateWorkOrder(serviceRequestRef, serviceCategory, organizationService, tracingService, userId);
            }

            var serviceRequest = context.IncidentSet.Single(r => r.Id == serviceRequestRef.Id);
            string lcid = GetUserLanguage(userId, organizationService);
            MessageTranslationManager translationManager = new MessageTranslationManager(this.context);


            // update Queue on service request and assign service request
            if (queueItemId != Guid.Empty && outputQueueRef != null && serviceRequest != null)
            {
                if (serviceCategory.Attributes.Contains("ifm_thirdpartyvendorid"))
                {
                    thirdparty_Vendor = serviceCategory.ifm_thirdpartyvendorid;
                    //"ThirdParty vendor identified: " + thirdparty_Vendor.Id);
                }

                UpdateServiceRequest(serviceRequest, outputQueueRef, processStageName, thirdparty_Vendor, tracingService); // Updating Queue
                //"Service Request updated with Queue");

                bool isCurrentUserAQueueMember = IsUserQueueMember(outputQueueRef.Id, userId, organizationService);
                //"Is Current User A Queue Member {0}", isCurrentUserAQueueMember);

                bool SRAssignresponse = false;

                // assign service request to current user (by picking the queue item) if current user is a queue member
                if (isCurrentUserAQueueMember)
                {
                    PickFromQueueRequest pickFromQueueRequest = new PickFromQueueRequest
                    {
                        QueueItemId = queueItemId,
                        WorkerId = userId
                    };
                    context.Execute(pickFromQueueRequest);

                }
                else 
                {
                    // assign service request to queue owner
                    SRAssignresponse = ServiceRequestAssignment(serviceRequest, outputQueueRef, userId, organizationService, out isAssigneeCurrentUser);
                }


                if (isCurrentUserAQueueMember)
                {
                    userMessage = string.Format(translationManager.GetTranslation(organizationService, tracingService, "SR005", lcid), outputQueueRef.Name);
                    if (thirdparty_Vendor != null && processStageName == ACTION && serviceCategory?.ifm_isworkorderrequired == false)
                    {
                        thirdPartyMessage = string.Format(translationManager.GetTranslation(organizationService, tracingService, "SR014", lcid), thirdparty_Vendor.Name, outputQueueRef.Name);
                        userMessage = thirdPartyMessage;
                    }
                }




                //Send Email Notification
                if (SRAssignresponse == true)
                {
                    EntityCollection queueMembers = this.ReturnQueueMembersByQueueName(adminService, outputQueueRef.Id);
                    if (queueMembers != null && queueMembers.Entities != null ||
                        queueMembers.Entities.Count > 0)
                    {
                        for (int i = 0; i < queueMembers.Entities.Count; i++)
                        {
                            tracingService.Trace("Internal Notification.");
                            Guid internalUserId = (Guid)queueMembers.Entities[i].Attributes["systemuserid"];
                            ContractNotificationConfigurationManager contractManager = new ContractNotificationConfigurationManager(this.context);

                            List<KeyValuePair<string, Guid>> toRecipient = new List<KeyValuePair<string, Guid>>();
                            toRecipient.Add(new KeyValuePair<string, Guid>(SystemUser.EntityLogicalName, internalUserId));

                            List<KeyValuePair<string, Guid>> ccRecipient = null;

                            contractManager.Send(organizationService, adminService,
                                null, tracingService,
                                userId,
                                serviceRequest.Id,
                                serviceRequest.LogicalName,
                                serviceRequest.OwnerId,
                                serviceRequest.ifm_contractid.Id,
                                serviceRequest.ifm_raisedforid.Id,
                                toRecipient,
                                ccRecipient,
                                contractManager.GetCurrency(serviceRequest.TransactionCurrencyId),
                                ifm_contractnotificationconfiguration_ifm_eventcode.SRQualification);

                        }
                    }
                }
                //User Message
                if (SRAssignresponse == true)
                {
                    //"Message received from incident assignment");
                    userMessage = string.Format(translationManager.GetTranslation(organizationService, tracingService, "SR006", lcid), outputQueueRef.Name);
                    if (thirdparty_Vendor != null && processStageName == ACTION && serviceCategory?.ifm_isworkorderrequired == false)
                    {
                        thirdPartyMessage = string.Format(translationManager.GetTranslation(organizationService, tracingService, "SR014", lcid), thirdparty_Vendor.Name, outputQueueRef.Name);
                        userMessage = thirdPartyMessage;
                    }
                }
                else
                {
                    userMessage = translationManager.GetTranslation(organizationService, tracingService, "SR007", lcid);
                }

            }
            return userMessage;
        }

        private bool ServiceRequestAssignment(
            Incident serviceRequest,
            EntityReference outputQueueRef,
            Guid userId,
            IOrganizationService organizationService,
            out bool isAssigneeCurrentUser)
        {
            bool SRassignResponse = false;
            isAssigneeCurrentUser = false;

            var queue = context.QueueSet.Single(r => r.Id == outputQueueRef.Id);
            if (queue != null)
            {
                Guid assigneeId = queue.OwnerId.Id;
                string owningEntityType =
                    queue.OwningTeam != null ? Team.EntityLogicalName : SystemUser.EntityLogicalName;

               //  var assignRequest = new AssignRequest
                //  {
                //      Assignee = new EntityReference(owningEntityType, assigneeId),
                 //     Target = new EntityReference(Incident.EntityLogicalName, serviceRequest.Id)
                  //};

                  //assignResponse = (AssignResponse) context.Execute(assignRequest); 

                Incident updatedSvcReq = new Incident()
                {
                    LogicalName = Incident.EntityLogicalName,
                    Id = serviceRequest.Id

                };
                updatedSvcReq.OwnerId = new EntityReference(owningEntityType, assigneeId);
                UpdateRequest svcReqUpdate = new UpdateRequest()
                {
                    Target = updatedSvcReq
                };
                context.Execute(svcReqUpdate);


                SRassignResponse = true;

                isAssigneeCurrentUser = (assigneeId == userId);
            }

            return SRassignResponse;
        }
        public void CreateWorkOrder(EntityReference serviceRequestRef, ifm_servicecategory serviceCategory,
           IOrganizationService organizationService, ITracingService tracingservice, Guid userId)
        {
            const int requestTypeQuotation = 224960000;
            const int requestTypeFeedbackOrCompliment = 224960003;
            const int requestTypeComplaint = 224960004;
            string workOrderTypeToSet = "RM";   // Reactive Maintenance

            try
            {
                var serviceRequest = context.IncidentSet.FirstOrDefault(x => (x.Id == serviceRequestRef.Id));
                tracingservice.Trace("Tracing started");

                var clientContract = context.ifm_contractSet.Single(x => (x.Id == serviceRequest.ifm_contractid.Id));
                //if (clientContract.ifm_pricelistid == null)
                //{
                //    throw new InvalidPluginExecutionException("Pricelist not found.");
                //}

                //tracingservice.Trace("Client Contract {0}", clientContract.ifm_pricelistid.Id.ToString());

                switch (serviceRequest.ifm_requesttypecode.Value)
                {
                    case requestTypeQuotation:
                        workOrderTypeToSet = "QUOTE";   // Quote
                        break;
                    case requestTypeFeedbackOrCompliment:
                    case requestTypeComplaint:
                        workOrderTypeToSet = "COM";     // Customer compliant / feedback
                        break;
                }

                //var workOrderType = context.msdyn_workordertypeSet.FirstOrDefault(w => (w.msdyn_name == "Reactive Maintenance"));
                var workOrderType =
                    context.msdyn_workordertypeSet.FirstOrDefault(w => (w.ifm_code == workOrderTypeToSet));
                tracingservice.Trace("workOrderType {0}", workOrderType?.ToEntityReference());

                var externalSystemName =
                    serviceCategory.GetOptionSetLabel(
                        "ifm_externalsystemlist",
                        serviceCategory?.ifm_externalsystemlist?.Value,
                        Constants.CRM.LanguageCodes.English,
                        organizationService);
                tracingservice.Trace("Get External system name");

                if (serviceRequest == null) return;

                EntityReference woPriceList = null;
                EntityReference woCurrency = null;
                var site = context.AccountSet.FirstOrDefault(a => a.AccountId.Value == serviceRequest.ifm_siteid.Id);
                if (site != null)
                {
                    if (site.DefaultPriceLevelId != null)
                    {
                        woPriceList = site.DefaultPriceLevelId;
                    }

                    // if (site.TransactionCurrencyId != null) to control, use the currency from pricelist. If the pricelist currency and Site currency is differnt then system will throw error.
                      //{
                        //  woCurrency = site.TransactionCurrencyId;
                      //}
    }

                if (woPriceList == null && clientContract.ifm_pricelistid != null)
                {
                    woPriceList = clientContract.ifm_pricelistid;
                }
string lcid = UserSettingManager.GetUserLanguage(userId, organizationService);
MessageTranslationManager translationManager = new MessageTranslationManager(this.context);

if (woPriceList == null && !string.IsNullOrEmpty(lcid))
{
    // Price list not found.
    throw new InvalidPluginExecutionException(translationManager.GetTranslation(organizationService, tracingservice, "SR010", lcid));
}

if (woCurrency == null)
{
    var priceList = context.PriceLevelSet.FirstOrDefault(a => a.PriceLevelId.Value == woPriceList.Id);
    if (priceList != null && priceList.TransactionCurrencyId != null)
    {
        woCurrency = priceList.TransactionCurrencyId;
    }
}
if (woCurrency == null && !string.IsNullOrEmpty(lcid))
{
    // Currency not found.
    throw new InvalidPluginExecutionException(translationManager.GetTranslation(organizationService, tracingservice, "SR011", lcid));
}

msdyn_workorder workOrder = new msdyn_workorder()
{
    msdyn_ServiceRequest = serviceRequestRef,
    msdyn_PrimaryIncidentType = serviceCategory?.ifm_incidenttypeId,
    msdyn_ServiceAccount = serviceRequest.ifm_propertyid != null ? serviceRequest.ifm_propertyid : serviceRequest.ifm_siteid,
    msdyn_WorkOrderType = workOrderType?.ToEntityReference(),
    msdyn_WorkOrderSummary = serviceRequest.Description,
    msdyn_ReportedByContact = serviceRequest.ifm_raisedforid,
    //msdyn_PriceList = priceList?.ToEntityReference(),
    msdyn_PriceList = woPriceList, //clientContract.ifm_pricelistid,
    TransactionCurrencyId = woCurrency,
    msdyn_Priority = serviceRequest.ifm_priorityid,
    ifm_clientcontractid = serviceRequest.ifm_contractid,
    ifm_propertyid = serviceRequest.ifm_propertyid,
    ifm_siteid = serviceRequest.ifm_siteid,
    ifm_servicecategorylevel1id = serviceRequest.ifm_servicecategorylevel1id,
    ifm_servicecategorylevel2id = serviceRequest.ifm_servicecategorylevel2id,
    ifm_servicecategorylevel3id = serviceRequest.ifm_servicecategorylevel3id,
    ifm_servicecategorylevel4id = serviceRequest.ifm_servicecategorylevel4id,
    ifm_building_externalareaid = serviceRequest.ifm_building_externalareaid,
    ifm_floor_roofid = serviceRequest.ifm_floor_roofid,
    ifm_wingid = serviceRequest.ifm_wingid,
    ifm_roomid = serviceRequest.ifm_roomid,
    ifm_isintegrated = false,
    ifm_workordertitle = serviceRequest.Title,
    ifm_hasmissingintegrationdata = true,
    ifm_externalsystemname = externalSystemName,
    ifm_servicecategorytranslated = serviceRequest.ifm_servicecategorytranslated,
    ifm_workordersummarytranslated = serviceRequest.ifm_descriptiontranslated,
    ifm_translationrequired = serviceRequest.ifm_translationrequired,
    ifm_providerduedate = serviceRequest.ifm_ProviderDueDate,
    ifm_financialtype = new OptionSetValue((int)WorkOrderManager.FINANCIALTYPE_CONTRACTUAL),
    ifm_profitcentre = GetProfitCentre(organizationService, tracingservice, serviceRequest),
    ifm_costthreshold = serviceRequest.ifm_costthreshold
};

context.AddObject(workOrder);
context.SaveChanges();
tracingservice.Trace("Create WO");
            }
            catch (Exception e)
{
    tracingservice.Trace("Exception: {0} {1}", e.Message, e.InnerException);
    throw;
}
        }
        public Guid CreateQueueItem(EntityReference serviceRequestRef, EntityReference outputQueueRef)
{
    AddToQueueRequest routeRequest = new AddToQueueRequest();
    routeRequest.Target = serviceRequestRef;
    routeRequest.DestinationQueueId = outputQueueRef.Id;

    // Look for existing queue item
    var matches =
        context.QueueItemSet
            .Where(x => (x.ObjectId.Id == serviceRequestRef.Id))
            .OrderByDescending(y => y.CreatedOn)
            .Select(z => new { z.QueueItemId, z.QueueId })
            .ToList();

    var existingQueueItem = matches.FirstOrDefault();
    // if there are other queueItems on the same queue for the same ServicerRequest, its a potential duplicate.
    var duplicatesOnSameQueue = matches.Where(x =>
        x.QueueId.Id == existingQueueItem.QueueId.Id && x.QueueItemId != existingQueueItem.QueueItemId);
    foreach (var queueItem in duplicatesOnSameQueue)
    {
        context.Execute(
            new RemoveFromQueueRequest()
            {
                QueueItemId = queueItem.QueueItemId.Value
            });
    }

    if (existingQueueItem != null) // This is to move from existing queue to new queue
    {
        routeRequest.SourceQueueId = existingQueueItem.QueueId.Id;
    }

    AddToQueueResponse response = (AddToQueueResponse)context.Execute(routeRequest);

    return response?.QueueItemId ?? Guid.Empty;
}

private void UpdateServiceRequest(Incident serviceRequest, EntityReference outputQueueRef, string processStageName, EntityReference thirdparty_Vendor, ITracingService tracingservice)
{
    Entity serviceRequestToUpdate = null;

    if (serviceRequest.ifm_queueid != null && serviceRequest.ifm_queueid.Id == outputQueueRef.Id)
        return;

    if (processStageName == ACTION && thirdparty_Vendor != null)
    {
        serviceRequestToUpdate = new Incident
        {
            ifm_queueid = outputQueueRef,
            ifm_thirdpartyvendorid = thirdparty_Vendor,
            Id = serviceRequest.Id
        };
    }
    else
    {
        //Otherwise update Queue
        serviceRequestToUpdate = new Incident { ifm_queueid = outputQueueRef, Id = serviceRequest.Id };
        tracingservice.Trace("Queue updated to SR record.");
    }
    context.UpdateObject(serviceRequestToUpdate);
}
*/

        /*
        public string GetTransitionType(Guid scL3or4IdImage, Guid scL3or4IdTarget, msdyn_workorder current, msdyn_workorder merged)
        {
            //tracingService.Trace("Enter GetTransitionType");

            string transitionType = string.Empty;
            var SR = context.IncidentSet.FirstOrDefault(r => r.ifm_workorderid.Id == current.Id);



            //Case of reclassification
            if (scL3or4IdImage != Guid.Empty
                && scL3or4IdTarget != Guid.Empty
                && scL3or4IdImage != scL3or4IdTarget)
            {
                var imageExternalSystemName = GetExternalSystemName(service, scL3or4IdImage);
                var targetExternalSystemName = GetExternalSystemName(service, scL3or4IdTarget);




                //Ravi Sonal: Check If service request is Resolved
                if (current.msdyn_SystemStatus != null
                    && (
                    SR != null 
                    && SR.StatusCode == incident_statuscode.ProblemSolved
                    && current.msdyn_SystemStatus.Value != WorkOrderManager.closedPosted
                    )
                    )
                {
                    throw new InvalidPluginExecutionException(
                        "Can not update Work Order. The related Service Request has been resolved.");
                }

                if (string.IsNullOrEmpty(imageExternalSystemName) 
                    && !string.IsNullOrEmpty(targetExternalSystemName) 
                    && !IsTransitive(tracingService, scL3or4IdImage) 
                    && !IsTransitive(tracingService, scL3or4IdTarget)
                    ) // Soft to Hard, No Transitive
                {
                    transitionType = RECLASSIFY_SOFTTOHARD;
                    //tracingService.Trace("Enter GetTransitionType Soft to Hard");

                    //Ravi Sonal: Check if these code is relevant
                    //current.ifm_externalsystemname = targetExternalSystemName;
                    //merged.ifm_externalsystemname = targetExternalSystemName;
                }
                else if (!string.IsNullOrEmpty(imageExternalSystemName) && !IsTransitive(tracingService, scL3or4IdImage) &&
                         IsTransitive(tracingService, scL3or4IdTarget)) // Hard to Transitive
                {
                    //tracingService.Trace("Enter GetTransitionType Hard to Transitive");

                    transitionType = RECLASSIFY_REROUTESR;
                    //tracingService.Trace("Send shared variable to Post Plugin: Reroute SR");
                }
                else if (IsTransitive(tracingService, scL3or4IdImage) && !IsTransitive(tracingService, scL3or4IdTarget) &&
                         string.IsNullOrEmpty(imageExternalSystemName)) // Transitive to Soft
                {
                    //tracingService.Trace("Enter GetTransitionType Transtive to Soft");
                    transitionType = RECLASSIFY_CANCELWO;
                    //tracingService.Trace("Send shared variable to Post Plugin: Cancel WO");
                }
                else if (string.IsNullOrEmpty(imageExternalSystemName)
                    && string.IsNullOrEmpty(targetExternalSystemName)
                    && !IsTransitive(tracingService, scL3or4IdImage)
                    && !IsTransitive(tracingService, scL3or4IdTarget)) // Soft to Soft,  No Transitive
                {
                    transitionType = RECLASSIFY_SOFTTOSOFT;
                    //tracingService.Trace("Enter GetTransitionType Soft to Soft");
                }
            }
            else if (current.Contains("msdyn_systemstatus") 
                && current.msdyn_SystemStatus.Value == WorkOrderManager.closedPosted)
            {
                //when D365 WO is 'closed-posted'
                //if WO is Soft FM Quote type then trigger custom action 'ifm_closesoftfmworkorder' for integration by setting treansitionType to WOQUOTE_CLOSEDPOSTED
                //Otherwise do nothing for other WO type

                var serviceCategoryId = merged.ifm_servicecategorylevel4id != null 
                    ? merged.ifm_servicecategorylevel4id.Id 
                    : (
                    merged.ifm_servicecategorylevel3id != null 
                    ? merged.ifm_servicecategorylevel3id.Id 
                    : (
                    merged.ifm_servicecategorylevel2id != null 
                    ? merged.ifm_servicecategorylevel2id.Id 
                    : Guid.Empty
                    )
                    );

                if (serviceCategoryId != Guid.Empty)
                {
                    var externalSystemNameSc = GetExternalSystemName(service, serviceCategoryId);

                    var workOrderType = context.msdyn_workordertypeSet.Single(a => a.msdyn_workordertypeId == merged.msdyn_WorkOrderType.Id);

                    // Conditions to check soft FM Quote WO
                    if (workOrderType != null && (workOrderType.msdyn_name.ToUpper() == WOTYPE_QUOTE) && (!string.IsNullOrEmpty(merged.ifm_purchaseorderno) && string.IsNullOrEmpty(externalSystemNameSc)))
                    {
                        //tracingService.Trace("Send shared variable to Post Plugin: Work Order with PO is Closed Posted");
                        transitionType = WorkOrderManager.WOQUOTE_CLOSEDPOSTED;
                    }
                }
                else
                {
                    //tracingService.Trace("Service Category is empty.");
                }
            }
            //tracingService.Trace("Exit GetTransitionType, value is set as: " + transitionType);
            return transitionType;
        }


        private string GetExternalSystemName(IOrganizationService service, Guid serviceCategoryID)
        {
            var serviceCategory = context.ifm_servicecategorySet.Single(r => r.Id == serviceCategoryID);

            var externalSystemName =
                serviceCategory.GetOptionSetLabel(
                    "ifm_externalsystemlist",
                    serviceCategory?.ifm_externalsystemlist?.Value,
                    Constants.CRM.LanguageCodes.English,
                    service);

            return externalSystemName;
        }
        */
    }
}
