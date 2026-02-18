using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Microsoft.Crm.Sdk.Messages;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Query;
using System.Diagnostics.CodeAnalysis;
using Sodexo.iFM.Shared.EntityController;

//using Microsoft.Xrm.Sdk.Client;
//Ravi SOnal: Cleanup Trace logging and have proper Format for it

namespace Sodexo.iFM.Plugins.Manager
{
    public class WorkOrderManager : ManagerBase
    {
        public IOrganizationService orgService;
        public IOrganizationService orgServiceAdmin;

        //Ravi Sonal: See if we can move this to Base class
        public WorkOrderRecord PreImage;
        public WorkOrderRecord PostImage;
        public WorkOrderRecord TargetImage;

        public WorkOrderManager(ILocalPluginContext localPluginContext)
           : base(localPluginContext)
        {
            orgService = this.LocalPluginContext.CurrentUserService;
            orgServiceAdmin = this.LocalPluginContext.SystemUserService;
        }
        public void runPreUpdateSync(Entity preImage, Entity postImage, Entity targetImage)
        {
            //Ravi Sonal: Remove dependency from Post Image
            //Ravi Sonal: Look for better structure for passing parameter

            loadPreChecks(preImage, postImage, targetImage);
            TraceMessage += "|Start: runPreUpdateSync|";
            //Context 1: Service category
            Guid serviceCategoryId = Guid.Empty;
            if (TargetImage.ServicecategoryLevel3 != null || TargetImage.ServicecategoryLevel4 != null)
            {
                serviceCategoryId = TargetImage.ServicecategoryLevel4 != null
                    ? TargetImage.ServicecategoryLevel4.Id : TargetImage.ServicecategoryLevel3.Id;
                TraceMessage += "|Service Category - Target: " + serviceCategoryId + "|";
            }
            else if (PreImage.ServicecategoryLevel3 != null)
            {
                serviceCategoryId = PreImage.ServicecategoryLevel4 != null
                    ? PreImage.ServicecategoryLevel4.Id : PreImage.ServicecategoryLevel3.Id;
                TraceMessage += "|Service Category - PreImage: " + serviceCategoryId + "|";
            }

            //Step 1: isRemovePriority
            if (!CheckPriorityChange())
            {
                TargetImage.Record.Attributes.Remove("msdyn_priority");
                TraceMessage += "|Priority Removed: Yes|";
            }

            //Step 2: No Service category
            //Ravi Sonal: Work on It, Create FCB for this 
            if (PreImage.ServicecategoryLevel1 == null
                && TargetImage.ServicecategoryLevel1 == null)
            {
                //For Work Order Type = Predictive Maintenance which does not have any Service Category
                string subject = "Work Order created without Service Category";
                this.CreateLog(subject, subject, TargetImage);
                //Ravi Sonal: see if return is correct
                TraceMessage += "|" + subject + "|";
                return;
            }
            //Step 2: Sync WO.CostThreshold with service category CostThreshold 
            //Check the logic behind it once. seems useless
            if (serviceCategoryId != Guid.Empty)
            {
                ServiceCategoryRecord SRCategory = new ServiceCategoryRecord(serviceCategoryId, LocalPluginContext.CurrentUserService);
                if (SRCategory.CostThreshold != PreImage.CostThreshold)
                {
                    TargetImage.CostThreshold = SRCategory.CostThreshold;
                }
                else
                {
                    TargetImage.Record.Attributes.Remove("ifm_costthreshold");
                }
                TraceMessage += "|Service Category Cost Threshold: " + SRCategory.CostThreshold + "|";
            }

            //Step 5:Sync SubStatus
            if (PreImage.SystemStatus == WorkOrderRecord.SystemStatusEnum.OpenUnscheduled
                && TargetImage.SystemStatus == WorkOrderRecord.SystemStatusEnum.OpenScheduled)
            {
                if (PreImage.SubStatus != null && TargetImage.SubStatus == null)
                {
                    //Ravi Sonal: Check if this is sending null to status change plugin
                    TargetImage.SubStatus = PreImage.SubStatus;
                    TraceMessage += "|WO Sub Status: " + PreImage.SubStatus + "|";
                }
            }

            //Ravi Sonal: Step 6: Originated in Dynamics
            //Ravi Sonal: Pending: Doubt onvalidity of this step
            //Ravi Sonal: Let the tester find this out first
            //var mergedRecord = preWorkOrderImage.Merge(target).ToEntity<msdyn_workorder>();
            //localContext.TracingService.Trace("Merge record");
            //if (!string.IsNullOrEmpty(mergedRecord.msdyn_name))
            //{
            //    localContext.TracingService.Trace("WO Number :" + mergedRecord.msdyn_name);
            //}
            //if (mergedRecord.ifm_originatedind365 == false) // No action if work order is not originated in D365.
            //    return;

            //Ravi Sonal: Check the placement
            //if (serviceCategoryLastIdImage == Guid.Empty
            //   && serviceCategoryLastIdTarget == Guid.Empty)
            //{
            //    return;
            //}
        }
        public void runPostUpdateSync(Entity preImage, Entity postImage, Entity targetImage)
        {
            //Ravi Sonal: Look for better structure for passing parameter
            loadPreChecks(preImage, postImage, targetImage);
            this.TraceMessage += "|Start: runPostUpdateSync|";

            //Check 1: WO Type Name
            string woTypeName = null;
            if (PostImage.WorkOrderType != null)
            {
                WorkOrderTypeRecord woType = new WorkOrderTypeRecord(PostImage.WorkOrderType.Id, this.LocalPluginContext.CurrentUserService);
                woTypeName = woType.Name;
            }
            this.TraceMessage += "|woTypeName: " + woTypeName + "|";

            //Check 2:PO Number
            string poNumber = null;
            if (!string.IsNullOrEmpty(PostImage.PurchaseOrderNumber))
            {
                poNumber = PostImage.PurchaseOrderNumber;
            }
            this.TraceMessage += "|poNumber: " + poNumber + "|";

            //Check 3: External System Name
            ServiceCategoryRecord.SendToExternalSystemEnum externalSystemNameIntegration = ServiceCategoryRecord.SendToExternalSystemEnum.Null;
            if (PostImage.ServicecategoryLevel3 != null)
            {
                Guid serviceCategoryId = PostImage.ServicecategoryLevel4 != null
                    ? PostImage.ServicecategoryLevel4.Id : PostImage.ServicecategoryLevel3.Id;

                ServiceCategoryRecord scRecord = new ServiceCategoryRecord(serviceCategoryId, this.LocalPluginContext.CurrentUserService);
                externalSystemNameIntegration = scRecord.SendToExternalSystem;
                this.TraceMessage += "|serviceCategoryId: " + serviceCategoryId.ToString() + "|";
            }
            this.TraceMessage += "|externalSystemNameIntegration: " + externalSystemNameIntegration.ToString() + "|";
            bool isPOQuotable = false;

            if (poNumber != null
                && woTypeName.ToUpper() == "QUOTE"
                && externalSystemNameIntegration != ServiceCategoryRecord.SendToExternalSystemEnum.Null)
            {
                isPOQuotable = true;
            }
            if (isPOQuotable && PostImage.SystemStatus != WorkOrderRecord.SystemStatusEnum.OpenUnscheduled)
            {
                isPOQuotable = false;
            }
            this.TraceMessage += "|isPOQuotable: " + isPOQuotable + "|";
            bool kpiChecks = false;
            bool hasOnHold = false;
            ServiceRequestRecord srRecord = null;
            if (PostImage.ServiceRequest != null)
                srRecord = new ServiceRequestRecord(PostImage.ServiceRequest.Id, this.LocalPluginContext.CurrentUserService);
            if (isPOQuotable)
            {
                if (srRecord != null
                    && srRecord.FirstResponseByKPIId != null
                    && srRecord.ResolveByKPIId != null
                    && hasOnHold)
                {
                    kpiChecks = true;
                    this.TraceMessage += "|srRecord.Id: " + srRecord.Id.ToString() + "|";
                }
            }
            this.TraceMessage += "|kpiChecks: " + kpiChecks + "|";
            if (kpiChecks)
            {
                hasOnHold = IsContractHaveOnHold(srRecord.Contract.Id);
            }
            this.TraceMessage += "|hasOnHold: " + hasOnHold + "|";
            if (kpiChecks && hasOnHold)
            {
                Entity serviceRequestToUpdate = new Entity
                {
                    LogicalName = ServiceRequestRecord.logicalName,
                    Id = srRecord.Id
                };
                serviceRequestToUpdate["statuscode"] = new OptionSetValue((int)ServiceRequestRecord.StatusCodeEnum.OnHold);
                this.LocalPluginContext.CurrentUserService.Update(serviceRequestToUpdate);
            }
            else if (isPOQuotable)
            {
                //Ravi Sonal: Check this WorkFlow
                TraceMessage += "|WorkFlow Name: IFM - PO Required Quotable SC - Change SR Status|";
                ExecuteWorkflowRequest workflowRequest = new ExecuteWorkflowRequest()
                {
                    WorkflowId = new Guid("0CBC914D-804F-4BCA-AFAD-EE148F889A27"),
                    EntityId = srRecord.Id
                };
                ExecuteWorkflowResponse workflowResponse = (ExecuteWorkflowResponse)this.LocalPluginContext.CurrentUserService.Execute(workflowRequest);
            }
            this.TraceMessage += "|End: runPostUpdateSync|";
        }
        public void runAsyncOnIncidentTypeChange(Entity preImage, Entity postImage, Entity targetImage)
        {
            // {Ravi Sonal}-Critical Note:
            /* See the usage of this plugin.
             * If its heavy, then seprate it into two (1 sync and 1 async) plugin
             * msdyn_primaryincidenttype: this can bemoved to pre update
             * msdyn_primaryincidentdescription: this can be moved to pre update
             * DeleteIncidentType/ Recreate IncidentType: or can be moved to separate plugin
             */
            loadPreChecks(preImage, postImage, targetImage);
            this.TraceMessage += "|Start: runAsyncOnIncidentTypeChange|";
            EntityReference serviceCategoryRef = getServiceCategoryContext(this.PostImage);
            Entity woToUpdate = new Entity()
            {
                LogicalName = "msdyn_workorder",
                Id = TargetImage.Id
            };
            EntityReference oldIncidentType = PreImage.PrimaryIncidentType;
            EntityReference newIncidentType = null;
            ServiceCategoryRecord SRCategory = null;
            IncidentTypeRecord incidentType = null;
            if (oldIncidentType != null)
                this.TraceMessage += "|oldIncidentType: " + oldIncidentType.Id + ": " + oldIncidentType.Name + "|";
            if (serviceCategoryRef != null)
            {
                SRCategory = new ServiceCategoryRecord(serviceCategoryRef.Id, orgService);
                newIncidentType = SRCategory.GetIncidentType(SRCategory, orgService);
                if (newIncidentType != null)
                {
                    incidentType = new IncidentTypeRecord(newIncidentType.Id, this.orgService);
                    this.TraceMessage += "|newIncidentType: " + newIncidentType.Id + ": " + newIncidentType.Name + "|";
                }
            }

            bool toDeleteWOIncident = false;

            if (oldIncidentType != null && newIncidentType == null)
            {
                //woToUpdate["msdyn_primaryincidenttype"] = null;
                woToUpdate["msdyn_primaryincidentdescription"] = null;
                toDeleteWOIncident = true;
                this.TraceMessage += "|msdyn_primaryincidenttype = NULL|";

            }
            else if (oldIncidentType == null && newIncidentType != null)
            {
                woToUpdate["msdyn_primaryincidenttype"] = newIncidentType;
                woToUpdate["msdyn_primaryincidentdescription"] = incidentType.Description;

                toDeleteWOIncident = true;
            }
            else if (oldIncidentType != null && newIncidentType != null && oldIncidentType.Id != newIncidentType.Id)
            {
                woToUpdate["msdyn_primaryincidenttype"] = newIncidentType;
                woToUpdate["msdyn_primaryincidentdescription"] = incidentType.Description;

                toDeleteWOIncident = true;
            }
            this.TraceMessage += "|toDeleteWOIncident: " + toDeleteWOIncident + "|";
            if (toDeleteWOIncident)
            {
                DeleteIncidentType(TargetImage.Id, oldIncidentType, LocalPluginContext.SystemUserService);
                RecreateIncidentType(TargetImage.Id, newIncidentType, LocalPluginContext.SystemUserService);
                this.orgService.Update(woToUpdate);
            }

            //Ravisonal: Currently a temporary SOlution.But will find a better place for it.
            //runAsyncWONotification(preImage, postImage, targetImage);


            if (toDeleteWOIncident)
            {
                this.orgService.Update(woToUpdate);
            }
            this.TraceMessage += "|End: runAsyncOnIncidentTypeChange|";
        }
        public void runAsyncOnStatusChange(Entity preImage, Entity postImage, Entity targetImage)
        {
            //Ravi Sonal: Check if below Status code can be 
            //if (subStatusID == Guid.Empty)
            //{
            //    tracingService.Trace("Create Log for sub-status is not found.");
            //    ifm_log log = new ifm_log()
            //    {
            //        Subject = string.Format("Update Service Request Sub Status to On-Hold"),
            //        Description = string.Format("Service Request Sub Status {0} is not found.", subStatusCode),
            //        ifm_isactionrequired = true,
            //        ifm_hasresolved = false,
            //        RegardingObjectId = new EntityReference(Incident.EntityLogicalName, workOrder.msdyn_ServiceRequest.Id)
            //    };
            //    context.AddObject(log);
            //    context.SaveChanges();
            //    return;
            //}

            //Ravi Sonal: Check this one
            /*
            if (currentImage.Contains("msdyn_substatus")
                && !currentTarget.Contains("msdyn_substatus")
                && currentImage.Contains("msdyn_systemstatus")
                && currentTarget.Contains("msdyn_systemstatus")
                && currentImage.msdyn_SystemStatus.Value == WorkOrderManager.openUnscheduled
                && currentTarget.msdyn_SystemStatus.Value == WorkOrderManager.openScheduled)
            {
                // Create Booking when WO is already awaiting material
                // Send Sub Status to Post
                currentTarget.msdyn_SubStatus = currentImage.msdyn_SubStatus;
            }
            */
            this.TraceMessage = "|Method: WorkOrderManager.runAsyncOnStatusChange: " + targetImage.Id.ToString() + "|";

            #region Prechecks
            //Checks
            bool isSRAvailable = false;
            bool isSystemStatusChange = false;
            bool isWOSubStatusChange = false;
            bool isMaximoIntegration = false;
            bool isContractOnHold = false;
            //Ravi Sonal: Did the fix for now, but make it nullable in future
            bool isSRActive = true;
            bool toUpdateSR = false;
            bool toUpdateWO = false;

            //Context
            ServiceRequestRecord serviceRequest = null;


            bool closeIncidentProblemSolved = false;
            //Ravi Sonal: Check this flag default value
            bool closeIncidentCancelled = false;
            //Ravi Sonal: Look for better structure for passing parameter
            loadPreChecks(preImage, postImage, targetImage);

            //Check 0: SubStatus
            if (TargetImage.Record.Contains("msdyn_substatus")
                && TargetImage.Record["msdyn_substatus"] != null)
            {
                isWOSubStatusChange = true;
                this.TraceMessage += "|Value: " + TargetImage.Record["msdyn_substatus"] + "|";
            }
            this.TraceMessage += "|sub Status Change: " + isWOSubStatusChange + " - Is value available" + TargetImage.Record.Contains("msdyn_substatus") + "|";
            if (TargetImage.SubStatus != null)
                this.TraceMessage += "|sub Status Name - " + TargetImage.SubStatus.Name + ": Sub Status Id - " + TargetImage.SubStatus.Id + "|";

            //Check 1: Service request
            if (PostImage.ServiceRequest != null)
            {
                //Check 1.1
                isSRAvailable = true;
                serviceRequest = new ServiceRequestRecord(PostImage.ServiceRequest.Id, this.LocalPluginContext.CurrentUserService);

                //Check 1.2: SR is not Active.
                if (serviceRequest.StateCode != ServiceRequestRecord.StateCodeEnum.Active)
                {
                    isSRActive = false;
                    //return;
                }
            }
            this.TraceMessage += "|isSRAvailable?: " + isSRAvailable + "|";
            if (isSRAvailable)
                this.TraceMessage += "|isSRActive?: " + isSRActive + ":" + serviceRequest.StateCode.ToString() + "|";



            //if (isSRAvailable)
            //{
            //Check 2:Status Status change
            //Ravi SOnal: Change the place
            if (PreImage.SystemStatus != PostImage.SystemStatus)
            {
                isSystemStatusChange = true;
            }
            //}
            this.TraceMessage += "|isSystemStatusChange: " + isSystemStatusChange + "|";
            this.TraceMessage += "|PreImage.SystemStatus: " + PreImage.SystemStatus.ToString() + "|";
            this.TraceMessage += "|PostImage.SystemStatus: " + PostImage.SystemStatus.ToString() + "|";
            //Check 3: Contract ON HOld Check
            if (isSRAvailable && isWOSubStatusChange)
            {
                isContractOnHold = IsContractHaveOnHold(serviceRequest.Contract.Id);
            }
            this.TraceMessage += "|isContractOnHold?: " + isContractOnHold + "|";
            //Check 4: If Maximo Integration
            if (isSystemStatusChange && !isMaximoIntegration)
            {
                //Ravi Sonal: As we have new integration to SMS, we need to check this logic
                //Assumption: COMP is not the first and only one update
                isMaximoIntegration = checkMaximoIntegrationExist(this.orgService, postImage.Id, null);
            }
            this.TraceMessage += "|isMaximoIntegration?: " + isMaximoIntegration + "|";



            //Context 1: Service category Post Image
            //Ravi Sonal: Think of a better way
            EntityReference serviceCategoryPostRef = null;
            if (PostImage.ServicecategoryLevel4 != null)
            {
                serviceCategoryPostRef = PostImage.ServicecategoryLevel4;
            }
            else if (PostImage.ServicecategoryLevel3 != null)
            {
                serviceCategoryPostRef = PostImage.ServicecategoryLevel3;
            }
            ServiceCategoryRecord srCategoryPostImage = null;
            if (serviceCategoryPostRef != null && serviceCategoryPostRef.Id != null)
            {
                srCategoryPostImage = new ServiceCategoryRecord(serviceCategoryPostRef.Id, this.orgService);
                this.TraceMessage += "|Post Service Category:" + serviceCategoryPostRef.Id + "|";
            }
            else
            {
                this.TraceMessage += "|Post Service Category: NULL |";
            }

            //Context 2: Work Order type
            //Ravi Sonal: Send this to entity Controlled record class
            //WO Type Name
            string woTypeName = null;
            if (PostImage.WorkOrderType != null)
            {
                WorkOrderTypeRecord woType = new WorkOrderTypeRecord(PostImage.WorkOrderType.Id, this.LocalPluginContext.CurrentUserService);
                woTypeName = woType.Name;

            }
            this.TraceMessage += "|woTypeName:" + woTypeName + "|";
            #endregion Prechecks

            #region Step 1: ActualCompletion Date to WO
            DateTime? actualCompletionDate = null;
            bool isActualCompletionDate = false;
            if (isSystemStatusChange)
            {
                if (PostImage.SystemStatus == WorkOrderRecord.SystemStatusEnum.OpenCompleted
                    && !isMaximoIntegration)
                {
                    actualCompletionDate = DateTime.UtcNow;
                    toUpdateWO = true;
                    isActualCompletionDate = true;
                }
                else if (PostImage.SystemStatus == WorkOrderRecord.SystemStatusEnum.ClosedPosted
                    || PostImage.SystemStatus == WorkOrderRecord.SystemStatusEnum.ClosedCanceled)
                {
                    if (!isMaximoIntegration && PostImage.ActualCompletionDate == null)
                    {
                        actualCompletionDate = DateTime.UtcNow;
                        toUpdateWO = true;
                        isActualCompletionDate = true;
                    }
                }
                else if (PreImage.SystemStatus == WorkOrderRecord.SystemStatusEnum.OpenCompleted)
                {
                    if (PostImage.SystemStatus == WorkOrderRecord.SystemStatusEnum.OpenUnscheduled
                        || PostImage.SystemStatus == WorkOrderRecord.SystemStatusEnum.OpenScheduled
                        || PostImage.SystemStatus == WorkOrderRecord.SystemStatusEnum.OpenInProgress)
                    {
                        actualCompletionDate = null;
                        toUpdateWO = true;
                        isActualCompletionDate = true;
                    }
                }
            }
            this.TraceMessage += "|Actual Completion Date Changed?: " + isActualCompletionDate + "|";
            this.TraceMessage += "|toUpdateWO: " + toUpdateWO + "|";
            this.TraceMessage += "|Actual Completion Date: " + actualCompletionDate.ToString() + "|";
            #endregion Step 1: ActualCompletion Date 

            #region Step 2: Sync SubStatus to SR
            string WOSubStatusName = null;
            string srSubStatusCode = null;
            EntityReference srSubStatusRef = null;
            bool issrSubStatusRefChanged = false;


            if (isWOSubStatusChange)
            {
                if (this.PostImage.SubStatus != null && this.PostImage.SubStatus.Name != null)
                {
                    WOSubStatusName = this.PostImage.SubStatus.Name;
                }
                else if (this.PostImage.SubStatus != null)
                {
                    WorkOrderSubStatusRecord woSubStatus = new WorkOrderSubStatusRecord(this.PostImage.SubStatus.Id, this.orgService);
                    WOSubStatusName = woSubStatus.Name;
                }
            }
            this.TraceMessage += "|WOSubStatusName: " + WOSubStatusName + "|";

            if (isSRAvailable && isSRActive && isSystemStatusChange && !String.IsNullOrEmpty(WOSubStatusName))
            {
                srSubStatusCode = getSRSubStatusCode(WOSubStatusName);
            }
            this.TraceMessage += "|srSubStatusCode: " + srSubStatusCode + "|";

            if (isContractOnHold && !String.IsNullOrEmpty(srSubStatusCode))
            {
                //Ravi Sonal: Convert this to Entity Controller Record class
                QueryExpression srSubStatusQuery = new QueryExpression()
                {
                    EntityName = "ifm_servicerequestsubstatus",
                    Criteria = new FilterExpression
                    {
                        Conditions =
                            {
                                new ConditionExpression("ifm_code", ConditionOperator.Equal, srSubStatusCode)
                            }
                    }
                };
                EntityCollection srSubStatusCollection = this.LocalPluginContext.CurrentUserService.RetrieveMultiple(srSubStatusQuery);
                Guid SRsubStatusID = srSubStatusCollection.Entities[0].Id;
                this.TraceMessage += "|srSubStatusRef: " + SRsubStatusID.ToString() + "|";
                srSubStatusRef = new EntityReference("ifm_servicerequestsubstatus", SRsubStatusID);
            }

            if (isSRAvailable
                && ((serviceRequest.SystemSubStatus == null && srSubStatusRef != null)
                || (serviceRequest.SystemSubStatus != null && srSubStatusRef == null)
                || (serviceRequest.SystemSubStatus != null && srSubStatusRef != null && serviceRequest.SystemSubStatus.Id != srSubStatusRef.Id)))
            {
                issrSubStatusRefChanged = true;
                toUpdateSR = true;
            }
            this.TraceMessage += "|issrSubStatusRefChanged: " + issrSubStatusRefChanged + "|";
            this.TraceMessage += "|toUpdateSR: " + toUpdateSR + "|";

            #endregion Step 2: Sync SubStatus to SR
            //Ravi Sonal: Time out issue
            this.LocalPluginContext.Trace(this.TraceMessage + "|Step 2 Time Stamp: " + DateTime.Now.ToString() + "|");
            this.TraceMessage = "";
            //Ravi Sonal: Timeout Issue end
            #region step 3: Sync Status to SR
            //Ravi SOnal: See if this doesnt have with below comment line
            //ServiceRequestRecord.StatusCodeEnum srStatusCode = ServiceRequestRecord.StatusCodeEnum.InProgress;
            ServiceRequestRecord.StatusCodeEnum srStatusCode = ServiceRequestRecord.StatusCodeEnum.Null;
            bool isSRStatusCodeChanged = false;

            if (isSRAvailable && isSRActive && !string.IsNullOrEmpty(WOSubStatusName))
            {
                if (isContractOnHold)
                {
                    srStatusCode = ServiceRequestRecord.StatusCodeEnum.OnHold;
                }
                else if (WOSubStatusName == WorkOrderSubStatusRecord.SubStatusName_AQA)
                {
                    srStatusCode = ServiceRequestRecord.StatusCodeEnum.OnHold;
                }

            }
            else if (isSRAvailable && isSRActive)
            {
                EntityCollection approvalColls = getWOApprovals(serviceRequest.Id, PostImage.Id);
                this.TraceMessage += "|getWOApprovals: " + approvalColls.Entities.Count + "|";
                if (approvalColls.Entities.Count > 0)
                {
                    Entity preApproval = approvalColls[0];
                    if (!preApproval.Contains("ifm_revisedfirstresponsefailuretime")
                        || !preApproval.Contains("ifm_revisedresolvedbyfailuretime")
                        || preApproval.GetAttributeValue<DateTime?>("ifm_revisedfirstresponsefailuretime") == DateTime.MinValue
                        || preApproval.GetAttributeValue<DateTime?>("ifm_revisedresolvedbyfailuretime") == DateTime.MinValue)
                    {
                        // SR back to In Progress after Revise SLA
                        srStatusCode = ServiceRequestRecord.StatusCodeEnum.OnHold;
                    }
                }
                else if (serviceRequest.WorkOrder != null && isContractOnHold)
                {
                    //Ravi Sonal: Need to find when this section is triggered
                    //Ravi SOnal: Move this array variable to a better place
                    //ifm_externalsystemstatus
                    string[] statusArray =
                    {
                        "WMATL",
                        "EREQ",
                        "EACCEPT",
                        "EREJECT"
                    };
                    EntityCollection woSTatusUpdateColls = getWorkOrderStatusUpdate(this.LocalPluginContext.CurrentUserService, PostImage.Id, null, statusArray);
                    this.TraceMessage += "|getWorkOrderStatusUpdate: " + woSTatusUpdateColls.Entities.Count + "|";
                    if (woSTatusUpdateColls.Entities.Count > 0)
                    {
                        srStatusCode = ServiceRequestRecord.StatusCodeEnum.OnHold;
                    }
                }
            }
            this.TraceMessage += "|srStatusCode: " + srStatusCode + "|";

            if (isSRAvailable && srStatusCode != ServiceRequestRecord.StatusCodeEnum.Null && serviceRequest.StatusCode != srStatusCode)
            {
                toUpdateSR = true;
                isSRStatusCodeChanged = true;
            }
            this.TraceMessage += "|toUpdateSR: " + toUpdateSR + "|";
            this.TraceMessage += "|isSRStatusCodeChanged: " + isSRStatusCodeChanged + "|";
            #endregion step 3: Sync Status to SR

            #region step 4: Sync WOOnHoldReason to SR

            string woOnHoldReason = PostImage.OnHoldReason;
            bool woOnHoldChange = false;
            if (!isContractOnHold)
            {
                woOnHoldReason = string.Empty;
            }

            if (this.TargetImage.Record.Contains("ifm_onholdreason")
                && !this.TargetImage.Record.Contains("msdyn_substatus"))
            {
                woOnHoldReason = this.TargetImage.OnHoldReason;
            }

            if (isSRAvailable && serviceRequest.OnHoldReason != woOnHoldReason
                    && !(string.IsNullOrEmpty(serviceRequest.OnHoldReason) && string.IsNullOrEmpty(woOnHoldReason)))
            {
                woOnHoldChange = true;
                toUpdateSR = true;
            }
            this.TraceMessage += "|woOnHoldChange: " + woOnHoldChange + "|";
            this.TraceMessage += "|toUpdateSR: " + toUpdateSR + "|";

            #endregion step 7: WO On Hold Reason
            //Ravi Sonal: Time out issue
            this.LocalPluginContext.Trace(this.TraceMessage + "|Step 4 Time Stamp: " + DateTime.Now.ToString() + "|");
            this.TraceMessage = "";
            //Ravi Sonal: Timeout Issue end
            #region Step 5: Sync First Response
            bool? firstResponseSent = null;
            bool processStatusSync = false;
            if (isSRAvailable && isSRActive && isSystemStatusChange)
            {
                processStatusSync = true;
            }
            else if (isSRAvailable && isSRActive
                && TargetImage.SystemStatus == WorkOrderRecord.SystemStatusEnum.OpenInProgress
                && PreImage.SystemStatus == WorkOrderRecord.SystemStatusEnum.OpenInProgress)
            {
                // Maximo Integration: INPRG to RESPONDED scenario
                processStatusSync = true;
            }
            this.TraceMessage += "|processStatusSync: " + processStatusSync + "|";

            if (processStatusSync
                && TargetImage.SystemStatus == WorkOrderRecord.SystemStatusEnum.OpenCompleted)
            {
                if (serviceRequest.FirstResponseSent != null
                    && !(bool)serviceRequest.FirstResponseSent)
                {
                    firstResponseSent = true;
                }
            }
            this.TraceMessage += "|firstResponseSent: " + firstResponseSent + "|";

            if (isSRAvailable && processStatusSync
                && TargetImage.SystemStatus == WorkOrderRecord.SystemStatusEnum.OpenInProgress)
            {
                // When the Work order is set to inprogress
                // If Soft FM : set the first response sent to true, else
                // If Hard FM and RESPONDED : set the first response sent to true.
                //Ravi Sonal: Find a better way to tell "Hard FM" vs "Soft FM"
                if (!isMaximoIntegration)
                {
                    //WO In Progress - Soft FM
                    firstResponseSent = true;
                }
                else
                {
                    // Hard - Maximo
                    if (checkMaximoIntegrationExist(this.LocalPluginContext.CurrentUserService, postImage.Id, "RESPONDED"))
                    {
                        //"WO In Progress - Hard FM"
                        firstResponseSent = true;
                    }
                }
            }
            if (isSRAvailable && serviceRequest.FirstResponseSent != firstResponseSent)
            {
                toUpdateSR = true;
            }
            this.TraceMessage += "|firstResponseSent: " + firstResponseSent + "|";
            this.TraceMessage += "|toUpdateSR: " + toUpdateSR + "|";

            #endregion Step 5: Sync First Response

            #region Step 6: Close Incident
            if (isSRAvailable && isSRActive
                && isSystemStatusChange
                && serviceRequest != null
                && TargetImage.SystemStatus == WorkOrderRecord.SystemStatusEnum.OpenCompleted)
            {
                closeIncidentProblemSolved = true;
            }
            if (isSRAvailable && isSRActive
                && isSystemStatusChange
                && TargetImage.SystemStatus == WorkOrderRecord.SystemStatusEnum.ClosedCanceled)
            {
                closeIncidentCancelled = true;
            }
            if (closeIncidentCancelled)
            {
                toUpdateSR = true;
            }
            this.TraceMessage += "|closeIncidentProblemSolved: " + closeIncidentProblemSolved + "|";
            this.TraceMessage += "|closeIncidentCancelled: " + closeIncidentCancelled + "|";
            this.TraceMessage += "|toUpdateSR: " + toUpdateSR + "|";
            #endregion Step 6: Close Incident
            //Ravi Sonal: Time out issue
            this.LocalPluginContext.Trace(this.TraceMessage + "|Step 6 Time Stamp: " + DateTime.Now.ToString() + "|");
            this.TraceMessage = "";
            //Ravi Sonal: Timeout Issue end
            #region Step 7: For close posted for integration
            // when D365 WO is 'closed-posted'
            // if WO is Soft FM Quote tyoe then trigger custom action 'ifm_closesoftfmworkorder' for integration by setting treansitionType to WOQUOTE_CLOSEDPOSTED
            // Otherwise do nothing for other WO type
            // Conditions to check soft FM Quote WO

            this.TraceMessage += "|PostImage.PurchaseOrderNumber: " + PostImage.PurchaseOrderNumber + "|";
            if (srCategoryPostImage != null)
                this.TraceMessage += "|srCategoryPostImage.SendToExternalSystem: " + srCategoryPostImage.SendToExternalSystem + "|";

            if (srCategoryPostImage != null && (int)srCategoryPostImage.SendToExternalSystem < 0)
            {
                srCategoryPostImage.SendToExternalSystem = ServiceCategoryRecord.SendToExternalSystemEnum.Null;
            }

            if (srCategoryPostImage != null
                && serviceCategoryPostRef != null
                && PostImage.SystemStatus == WorkOrderRecord.SystemStatusEnum.ClosedPosted)
            {
                //string transitionType = null;
                if (woTypeName != null
                    && woTypeName.ToUpper() == "QUOTE"
                    && !string.IsNullOrEmpty(PostImage.PurchaseOrderNumber)
                    && srCategoryPostImage.SendToExternalSystem == ServiceCategoryRecord.SendToExternalSystemEnum.Null
                    && serviceCategoryPostRef != null)
                {
                    // transitionType = "WO_Quote_ClosedPosted";
                    //this.TraceMessage += "|Transition Type: WO_Quote_ClosedPosted|";
                    //if (!string.IsNullOrEmpty(transitionType) && transitionType == "WO_Quote_ClosedPosted")
                    //{
                    //if ()
                    //{
                    this.TraceMessage += "|ifm_closesoftfmworkorder: Start|";
                    OrganizationRequest orgRequest = new OrganizationRequest("ifm_closesoftfmworkorder");
                    orgRequest["WorkOrder"] = new EntityReference(PostImage.LogicalName, PostImage.Id);
                    orgRequest["Target"] = new EntityReference(PostImage.LogicalName, PostImage.Id);
                    OrganizationResponse orgResponse = this.LocalPluginContext.CurrentUserService.Execute(orgRequest);
                    this.TraceMessage += "|ifm_closesoftfmworkorder: End|";
                    //}
                    //}
                }
            }


            #endregion Step 7: For close posted

            #region Step 7: Sync WO "Reason for Cancellation" to SR 
            //ifm_reasonforcancellation
            bool isReasonForCancellation = false;
            if (TargetImage.ReasonForCancellation != null)
            {
                //Ravi Sonal: keepin in sync with code pattern of previous section
                isReasonForCancellation = true;
                toUpdateSR = true;
                this.TraceMessage += "|isReasonForCancellation: " + isReasonForCancellation + "-" + TargetImage.ReasonForCancellation.Value.ToString() + "|";
            }
            #endregion Step 7: Sync WO "Reason for Cancellation" to SR 

            #region update WO 
            //Ravi Sonal: for now only ifm_actualcompletiondate
            if (toUpdateWO && isActualCompletionDate)
            {
                Entity woToUpdate = new Entity
                {
                    LogicalName = WorkOrderRecord.logicalName,
                    Id = PostImage.Id
                };
                if (isActualCompletionDate)
                {
                    woToUpdate["ifm_actualcompletiondate"] = actualCompletionDate;
                }

                this.LocalPluginContext.CurrentUserService.Update(woToUpdate);
                this.TraceMessage += "|WO Updated|";
            }
            #endregion update WO

            //Ravi Sonal: Time out issue
            this.LocalPluginContext.Trace(this.TraceMessage + "|Step Wo Update Time Stamp: " + DateTime.Now.ToString() + "|");
            this.TraceMessage = "";
            //Ravi Sonal: Timeout Issue end


            #region Update Service Request
            //String: onHoldReason
            //Entity Reference: serviceRequest.SystemSubStatus <-- workOrderSubStatus
            //OPtionSet: Status Code
            if (isSRAvailable && toUpdateSR)
            {
                Entity serviceRequestToUpdate = new Entity
                {
                    LogicalName = ServiceRequestRecord.logicalName,
                    Id = PostImage.ServiceRequest.Id
                };

                if (isReasonForCancellation)
                {
                    serviceRequestToUpdate["ifm_reasonforcancellation"] = TargetImage.ReasonForCancellation;
                }
                if (issrSubStatusRefChanged)
                {
                    serviceRequestToUpdate["ifm_systemsubstatusid"] = srSubStatusRef;
                }
                if (isSRStatusCodeChanged)
                {
                    serviceRequestToUpdate["statuscode"] = new OptionSetValue((int)srStatusCode);
                }
                if (woOnHoldChange)
                {
                    serviceRequestToUpdate["ifm_onholdreason"] = woOnHoldReason;
                }
                if ((firstResponseSent != null && (bool)firstResponseSent))
                {
                    serviceRequestToUpdate["firstresponsesent"] = firstResponseSent;
                }
                if (closeIncidentCancelled)
                {
                    serviceRequestToUpdate["statecode"] = new OptionSetValue((int)ServiceRequestRecord.StateCodeEnum.Canceled);
                    serviceRequestToUpdate["statuscode"] = new OptionSetValue((int)ServiceRequestRecord.StatusCodeEnum.Canceled);
                }
                this.TraceMessage += "|SR To Be Updated|";
                this.LocalPluginContext.CurrentUserService.Update(serviceRequestToUpdate);
                this.TraceMessage += "|SR Updated|";
            }
            #endregion Update Service Request
            //Ravi Sonal: Time out issue
            this.LocalPluginContext.Trace(this.TraceMessage + "|Step SR Update Time Stamp: " + DateTime.Now.ToString() + "|");
            this.TraceMessage = "";
            //Ravi Sonal: Timeout Issue end
            #region Close Incident
            //Ravi Sonal: Moving this to Work Flow to remove dependency from the code
            /*
            if (isSRAvailable && closeIncidentProblemSolved)
            {
                Entity incidentResolution = new Entity("incidentresolution");
                incidentResolution.Attributes.Add("incidentid", new EntityReference("incident", serviceRequest.Id));
                incidentResolution.Attributes.Add("subject", "");

                CloseIncidentRequest toCloseSR = new CloseIncidentRequest
                {
                    IncidentResolution = incidentResolution,
                    Status = new OptionSetValue((int)ServiceRequestRecord.StatusCodeEnum.ProblemSolved)
                };
                this.LocalPluginContext.CurrentUserService.Execute(toCloseSR);
                this.TraceMessage += "|Incident Closed as Problem Resolved|";
            }
            */
            #endregion CLose Incident
            //Ravi Sonal: Time out issue
            this.LocalPluginContext.Trace(this.TraceMessage + "|Step Close Incident Time Stamp: " + DateTime.Now.ToString() + "|");
            this.TraceMessage = "";
            //Ravi Sonal: Timeout Issue end
        }
        public void runAsyncOnOwnerChange(Entity preImage, Entity postImage, Entity targetImage)
        {
            //Ravi Sonal: Look for better structure for passing parameter
            loadPreChecks(preImage, postImage, targetImage);
            this.TraceMessage = "|Start Method: WorkOrderManager.runAsyncOnOwnerChange|";
            //Check Wo has Bookings and Resource requirements.
            //If the ownerof these are different than WO owner? then use the same WO
            if (TargetImage.Record.Contains("ownerid"))
            {
                getAllBookingsbyWorkorder(TargetImage);
                getAllResourceRequirements(TargetImage);

                //MAL-37 - Sharing an WO record with site ownership team if it is owned by individual user

                Entity WODetails = this.LocalPluginContext.SystemUserService.Retrieve(TargetImage.LogicalName, TargetImage.Record.Id, new ColumnSet("ifm_siteid"));
                if (WODetails != null && WODetails.Contains("ifm_siteid") && WODetails.Attributes["ifm_siteid"] != null)
                {
                    Guid siteId = WODetails.GetAttributeValue<EntityReference>("ifm_siteid").Id;
                    Entity siteDetails = this.LocalPluginContext.SystemUserService.Retrieve("account", siteId, new ColumnSet("ownerid"));
                    if (siteDetails != null && siteDetails.Contains("ownerid") && siteDetails.Attributes["ownerid"] != null)
                    {
                        Guid siteOwnerId = siteDetails.GetAttributeValue<EntityReference>("ownerid").Id;
                        Guid workOrderOwnerId = TargetImage.Record.GetAttributeValue<EntityReference>("ownerid").Id;
                        this.TraceMessage = "|SiteOwnerId : |" + siteOwnerId.ToString() + "|WOOwnerId : |" + workOrderOwnerId.ToString();
                        if (siteOwnerId != workOrderOwnerId)
                        {
                            ShareWORecordWithTeam(TargetImage.Record, siteOwnerId);
                        }
                        //else if (siteOwnerId == workOrderOwnerId)
                        //{
                        //    UnShareWORecordWithTeam(TargetImage.Record, siteOwnerId);
                        //}
                    }
                }
            }

           

            this.TraceMessage += "|End Method: WorkOrderManager.runAsyncOnOwnerChange|";
        }
        public void runAsyncWONotification(Entity preImage, Entity postImage, Entity targetImage)
        {
            //Ravi Sonal: Look for better structure for passing parameter
            loadPreChecks(preImage, postImage, targetImage);

            this.TraceMessage += "|START: Method - runAsyncWONotification|";


            //Check 1: Service request
            ServiceRequestRecord serviceRequest = null;
            if (PostImage.ServiceRequest != null)
            {
                serviceRequest = new ServiceRequestRecord(PostImage.ServiceRequest.Id, this.orgService);

                if (PostImage.SystemStatus != WorkOrderRecord.SystemStatusEnum.Null
                    && PostImage.SystemStatus != WorkOrderRecord.SystemStatusEnum.ClosedPosted
                    && serviceRequest != null
                    && serviceRequest.StatusCode == ServiceRequestRecord.StatusCodeEnum.ProblemSolved)
                {
                    throw new InvalidPluginExecutionException(
                        "Can not update Work Order. The related Service Request has been resolved.");
                }
            }

            //Check 2: Service category
            //Ravi Sonal: Think of a better way
            EntityReference serviceCategoryPostRef = null;
            if (PostImage.ServicecategoryLevel4 != null)
            {
                serviceCategoryPostRef = PostImage.ServicecategoryLevel4;
            }
            else if (PostImage.ServicecategoryLevel3 != null)
            {
                serviceCategoryPostRef = PostImage.ServicecategoryLevel3;
            }
            ServiceCategoryRecord srCategoryPostImage = null;
            if (serviceCategoryPostRef != null && serviceCategoryPostRef.Id != null)
            {
                srCategoryPostImage = new ServiceCategoryRecord(serviceCategoryPostRef.Id, this.orgService);
                this.TraceMessage += "|Post Service Category:" + srCategoryPostImage.Id + "|";
            }
            else
            {
                this.TraceMessage += "|Post Service Category: NULL |";
            }

            EntityReference serviceCategoryPreRef = null;
            if (PreImage.ServicecategoryLevel4 != null)
            {
                serviceCategoryPreRef = PreImage.ServicecategoryLevel4;
            }
            else if (PreImage.ServicecategoryLevel3 != null)
            {
                serviceCategoryPreRef = PreImage.ServicecategoryLevel3;
            }

            ServiceCategoryRecord srCategoryPreImage = null;
            if (serviceCategoryPreRef != null && serviceCategoryPreRef.Id != null)
            {
                srCategoryPreImage = new ServiceCategoryRecord(serviceCategoryPreRef.Id, this.orgService);
                this.TraceMessage += "|Pre Service Category:" + srCategoryPreImage.Id + "|";
            }
            else
            {
                this.TraceMessage += "|Pre Service Category: NULL |";
            }

            if (serviceCategoryPostRef == null && serviceCategoryPreRef == null)
            {
                return;
            }

            //SQR-13 - Maximo Wo order cancel issue.
            if (srCategoryPreImage != null)
            {
                string transitiveCode = getTransitiveServiceCategoryCode();
                bool isTransitive = srCategoryPreImage.isTransitive(transitiveCode);
                if (isTransitive)
                {
                   
                    Entity serviceCategoryRecord = this.LocalPluginContext.CurrentUserService.Retrieve("ifm_servicecategory", srCategoryPreImage.Id, new ColumnSet("ifm_parentid"));
                    if (serviceCategoryRecord != null && serviceCategoryRecord.Contains("ifm_parentid"))
                    {
                        Guid parentServiceCategory = serviceCategoryRecord.GetAttributeValue<EntityReference>("ifm_parentid").Id;
                        this.TraceMessage += "|Parent Service Category:" + serviceCategoryRecord.GetAttributeValue<EntityReference>("ifm_parentid").Id;
                        if (parentServiceCategory == serviceCategoryPostRef.Id)
                        {
                            this.TraceMessage += " | Level4 Service category is updated to Blank - No actions to be taken.";
                            return;
                        }                       
                    }
                }
            }
           

            //Check 3: Work Order type
            //Ravi Sonal: Send this to entity Controlled record class
            //WO Type Name
            string woTypeName = null;
            if (PostImage.WorkOrderType != null)
            {
                WorkOrderTypeRecord woType = new WorkOrderTypeRecord(PostImage.WorkOrderType.Id, this.LocalPluginContext.CurrentUserService);
                woTypeName = woType.Name;

            }

            //Get SMS Reclassification Flag
            bool isSMSReclassification = false;
            EntityReference level1PreServiceCategoryRef = null;
            EntityReference level1PostServiceCategoryRef = null;
            string externalSystemRecordId = null;
            if (preImage != null)
            {
                level1PreServiceCategoryRef = PreImage.ServicecategoryLevel1;
                externalSystemRecordId = PreImage.ExternalSystemRecordUID;
            }
            if (PostImage != null)
            {
                isSMSReclassification = PostImage.IsSMSReclassification;
                level1PostServiceCategoryRef = PostImage.ServicecategoryLevel1;
            }

            
            string transitionType = null;
            //Ravi Sonal: Moved this code to Status change plugin
            //Step 1
            //if (PostImage.SystemStatus == WorkOrderRecord.SystemStatusEnum.ClosedPosted
            //    && serviceCategoryPostRef != null)
            //{
            //    if (woTypeName != null
            //        && woTypeName.ToUpper() == "QUOTE"
            //        && !string.IsNullOrEmpty(PostImage.PurchaseOrderNumber)
            //        && srCategoryPostImage.SendToExternalSystem != ServiceCategoryRecord.SendToExternalSystemEnum.Null)
            //    {
            //        // when D365 WO is 'closed-posted'
            //        // if WO is Soft FM Quote tyoe then trigger custom action 'ifm_closesoftfmworkorder' for integration by setting treansitionType to WOQUOTE_CLOSEDPOSTED
            //        // Otherwise do nothing for other WO type
            //        // Conditions to check soft FM Quote WO
            //        transitionType = "WO_Quote_ClosedPosted";
            //    }
            //}

            string preExternalSystemName = null;
            if (srCategoryPreImage != null)
            {
                preExternalSystemName = Enum.GetName(typeof(ServiceCategoryRecord.SendToExternalSystemEnum), (int)srCategoryPreImage.SendToExternalSystem);
            }

            OptionSetValue eventCode = null;
            bool isReRouteSR = false;
            if (isSMSReclassification == false && preExternalSystemName != Enum.GetName(typeof(ServiceCategoryRecord.SendToExternalSystemEnum), ServiceCategoryRecord.SendToExternalSystemEnum.SMSV1))
            {
                this.TraceMessage += "|Transition Type:" + transitionType + "|";

                //Check 5: Transition Type
                if (srCategoryPreImage != null && srCategoryPostImage != null)
                    transitionType = GetTransitionType(srCategoryPreImage, srCategoryPostImage);

                this.TraceMessage += "|Transition Type:" + transitionType + "|";

                if (!string.IsNullOrEmpty(transitionType) && transitionType == "RerouteSR")
                {
                    if (serviceCategoryPostRef != null && serviceCategoryPreRef != null)
                    {
                        eventCode = new OptionSetValue((int)ContractNotificationConfigurationsRecord.EventCodeEnum.SRReclassificationHardToSoft);
                    }
                }
                if (!string.IsNullOrEmpty(transitionType) && transitionType == "SoftToSoft")
                {
                    if (serviceCategoryPostRef != null && serviceCategoryPreRef != null)
                    {
                        eventCode = new OptionSetValue((int)ContractNotificationConfigurationsRecord.EventCodeEnum.SRReclassificationSoftToSoft);
                    }
                }
                if (!string.IsNullOrEmpty(transitionType) && transitionType == "SoftToHard")
                {
                    if (serviceCategoryPostRef != null && serviceCategoryPreRef != null)
                    {
                        eventCode = new OptionSetValue((int)ContractNotificationConfigurationsRecord.EventCodeEnum.SRReclassificationSoftToHard);
                    }
                }
                if (!string.IsNullOrEmpty(transitionType) && transitionType == "CancelExternalWO")
                {
                    if (serviceCategoryPostRef != null && serviceCategoryPreRef != null)
                    {
                        eventCode = new OptionSetValue((int)ContractNotificationConfigurationsRecord.EventCodeEnum.SRReclassificationHardToSoft);
                        callCancelExternalWorkOrderAction(preExternalSystemName,externalSystemRecordId);
                        //Update External System Ref Id as null
                        WorkOrderRecord workOrder = new WorkOrderRecord(PostImage.Id, this.LocalPluginContext.CurrentUserService);
                        workOrder.ExternalSystemRecordUID = null;
                        workOrder.Update();
                    }
                }
            }
            else if (isSMSReclassification == true && srCategoryPostImage != null && srCategoryPreImage != null)
            {
                WorkOrderRecord workOrder = new WorkOrderRecord(PostImage.Id, this.LocalPluginContext.CurrentUserService);
                this.TraceMessage += "|Entered SMS Reclassification|";
                if (srCategoryPostImage.SendToExternalSystem == ServiceCategoryRecord.SendToExternalSystemEnum.SMSV1)
                {
                    this.TraceMessage += "|Reclassified Catgory is SMS integration|";                    
                }
                else
                {
                    //Cancel SMS existing work order.. Calling Action to cancel it.
                    callCancelExternalWorkOrderAction(preExternalSystemName, externalSystemRecordId);
                    this.TraceMessage += "|Reclassified Catgory is not SMS - Cancelling existing work Order|";                  
                    workOrder.ExternalSystemRecordUID = null;  //Remove Link in WO
                    
                }
                workOrder.IsSMSReclassification = false;
                workOrder.ExternalSystemName = srCategoryPostImage.SendToExternalSystem != ServiceCategoryRecord.SendToExternalSystemEnum.Null ? Enum.GetName(typeof(ServiceCategoryRecord.SendToExternalSystemEnum), (int)srCategoryPostImage.SendToExternalSystem):null;
                workOrder.Update();
                this.TraceMessage += "|New External System Name: " + workOrder.ExternalSystemName +"|";
                transitionType = GetSMSReclassTransitionType(level1PreServiceCategoryRef, level1PostServiceCategoryRef);
                if (!string.IsNullOrEmpty(transitionType) && transitionType == "SoftToSoft")
                {
                    if (serviceCategoryPostRef != null && serviceCategoryPreRef != null)
                    {
                        eventCode = new OptionSetValue((int)ContractNotificationConfigurationsRecord.EventCodeEnum.SRReclassificationSoftToSoft);
                    }
                }
                if (!string.IsNullOrEmpty(transitionType) && transitionType == "SoftToHard")
                {
                    if (serviceCategoryPostRef != null && serviceCategoryPreRef != null)
                    {
                        eventCode = new OptionSetValue((int)ContractNotificationConfigurationsRecord.EventCodeEnum.SRReclassificationSoftToHard);
                    }
                }
            }

            //This one is moved to status change plugin. Delete this after successfull test
            //if (!string.IsNullOrEmpty(transitionType) && transitionType == "WO_Quote_ClosedPosted")
            //{
            //    if (serviceCategoryPostRef != null && serviceCategoryPreRef != null)
            //    {
            //        this.TraceMessage += "|ifm_closesoftfmworkorder: Start|";
            //        OrganizationRequest orgRequest = new OrganizationRequest("ifm_closesoftfmworkorder");
            //        orgRequest["WorkOrder"] = new EntityReference(PostImage.LogicalName, PostImage.Id);
            //        orgRequest["Target"] = new EntityReference(PostImage.LogicalName, PostImage.Id);
            //        OrganizationResponse orgResponse = this.LocalPluginContext.CurrentUserService.Execute(orgRequest);
            //        this.TraceMessage += "|ifm_closesoftfmworkorder: End|";
            //    }
            //}

            this.TraceMessage += "|Event code:" + eventCode + "|";


            //}
            this.TraceMessage += "|Re Route SR:" + isReRouteSR + "|";
            //Check 1 : Get Queue Details
            string processStageName = "Action";
            EntityReference newQueueRef = null;
            EntityReference oldQueueRef = null;
            QueueRecord newQueue = null;
            if (srCategoryPostImage != null)
            {
                newQueueRef = srCategoryPostImage.GetOutputQueue(processStageName);
                this.TraceMessage += "|New Queue:" + newQueueRef.Id + "|";
                newQueue = new QueueRecord(newQueueRef.Id, this.orgServiceAdmin);
            }
            if (srCategoryPreImage != null)
            {
                oldQueueRef = srCategoryPreImage.GetOutputQueue(processStageName);
                this.TraceMessage += "|Old Queue:" + oldQueueRef.Id + "|";
            }



            //Step 1: Update Service request Owner based on Queue
            //Ravi Sonal: Move this owner update code to some other place
            if (serviceRequest != null)
            {

                this.TraceMessage += "|SR to Update: " + serviceRequest.Id.ToString() + "|";

                if (newQueueRef != null && oldQueueRef != null && newQueueRef.Id != oldQueueRef.Id)
                {
                    Entity serviceRequestToUpdate = new Entity
                    {
                        LogicalName = ServiceRequestRecord.logicalName,
                        Id = serviceRequest.Id
                    };

                    serviceRequestToUpdate["ifm_queueid"] = newQueueRef;
                    this.TraceMessage += "|SR Queue to Update: " + newQueueRef.Id.ToString() + "-" + newQueueRef.Name + "|";

                    if (newQueue.Owner != null && serviceRequest.Owner != null && serviceRequest.Owner.Id != newQueue.Owner.Id)
                    {
                        serviceRequestToUpdate["ownerid"] = newQueue.Owner;
                        this.TraceMessage += "|SR new Owner" + newQueue.Owner.Id.ToString() + "-" + newQueue.Owner.Name + "|";
                    }

                    this.TraceMessage += "|SR Updated with new Owner & Queue 1|";
                    try
                    {
                        //UpdateRequest test = new UpdateRequest()
                        //{
                        //    Target = serviceRequestToUpdate
                        //};
                        //test.Parameters.Add("BypassCustomPluginExecution", true);
                        //UpdateResponse test1 = (UpdateResponse)this.orgService.Execute(test);
                        this.orgService.Update(serviceRequestToUpdate);
                    }
                    catch (Exception ex)
                    {
                        this.TraceMessage += "|" + ex.InnerException + ex.Message + ex.StackTrace + "|";
                        throw new InvalidPluginExecutionException("Error in Updating SR Owner: " + ex.InnerException + ex.Message + ex.StackTrace);
                    }
                    this.TraceMessage += "|SR Updated with new Owner & Queue 2|";
                }
            }

            this.TraceMessage += "|ToSendNotification|";
            //Step 2: Send Notification
            if (eventCode != null)
            {
                this.TraceMessage += "|EventCode:" + eventCode.Value.ToString() + " | ";
                EmailNotificationManager sendEmailNotifications = new EmailNotificationManager(this.LocalPluginContext);
                if (newQueueRef != null && serviceRequest != null && oldQueueRef != null && newQueueRef.Id != oldQueueRef.Id)
                {
                    EntityCollection queueMembers = newQueue.getQueueMembers();
                    foreach (Entity record in queueMembers.Entities)
                    {
                        Guid queueMemberId = (Guid)record["systemuserid"];
                        sendEmailNotifications.ToRecipient.Add(new EntityReference(SystemUserRecord.logicalName, queueMemberId));
                    }
                }
                this.TraceMessage += "|To Recepient Count:" + sendEmailNotifications.ToRecipient.Count + "|";

                if (sendEmailNotifications.ToRecipient.Count > 0)
                {
                    sendEmailNotifications.LoadRegardingRecord(PostImage.Record, eventCode);
                }
            }
            this.TraceMessage += "|END: Method - runAsyncWONotification|";
            this.LocalPluginContext.Trace(this.TraceMessage);
        }

        public void runAsyncShareWO(Entity preImage, Entity postImage, Entity targetImage)
        {

            loadPreChecks(preImage, postImage, targetImage);
            this.TraceMessage = "|Start Method: WorkOrderManager.runAsyncShareWO|";

            if (TargetImage.Record.Contains("ownerid") && TargetImage.Record.Contains("ifm_siteid"))
            {
                Guid siteId = TargetImage.Record.GetAttributeValue<EntityReference>("ifm_siteid").Id;
                Entity siteDetails = this.LocalPluginContext.SystemUserService.Retrieve("account", siteId, new ColumnSet("ownerid"));
                if (siteDetails != null && siteDetails.Contains("ownerid") && siteDetails.Attributes["ownerid"] != null)
                {
                    Guid siteOwnerId = siteDetails.GetAttributeValue<EntityReference>("ownerid").Id;
                    Guid workOrderOwnerId = TargetImage.Record.GetAttributeValue<EntityReference>("ownerid").Id;
                    this.TraceMessage = "|SiteOwnerId : |" + siteOwnerId.ToString() + "|WOOwnerId : |" + workOrderOwnerId.ToString();
                    if (siteOwnerId != workOrderOwnerId)
                    {
                        this.TraceMessage = "Share Record Start";
                        ShareWORecordWithTeam(TargetImage.Record, siteOwnerId);
                        this.TraceMessage = "Share Record End";
                    }
                    //else if (siteOwnerId == workOrderOwnerId)
                    //{
                    //    this.TraceMessage = "UnShare Record Start";
                    //    UnShareWORecordWithTeam(TargetImage.Record, siteOwnerId);
                    //    this.TraceMessage = "UnShare Record End";
                    //}
                }
                              
            }
            this.TraceMessage += "|End Method: WorkOrderManager.runAsyncShareWO|";
        }
        private  void ShareWORecordWithTeam( Entity entity, Guid teamId)
        {
            
            var grantAccessRequest = new GrantAccessRequest
            {
                PrincipalAccess = new PrincipalAccess
                {
                    AccessMask = AccessRights.ReadAccess,
                    Principal = new EntityReference("team",teamId)
                },
                Target = new EntityReference(entity.LogicalName, entity.Id)
            };
            this.LocalPluginContext.SystemUserService.Execute(grantAccessRequest);
           
        }

        private void UnShareWORecordWithTeam(Entity entity, Guid teamId)
        {
            var revokeUserAccessReq = new RevokeAccessRequest
            {
                Revokee = new EntityReference("team",teamId),
                Target = new EntityReference(entity.LogicalName, entity.Id)
            };
            this.LocalPluginContext.SystemUserService.Execute(revokeUserAccessReq);           

        }

        private void loadPreChecks()
        {
            if (this.LocalPluginContext.TargetEntity != null)
                this.PostImage = new WorkOrderRecord(this.LocalPluginContext.TargetEntity, this.LocalPluginContext.CurrentUserService);

            if (this.LocalPluginContext.PreImage != null)
                this.PreImage = new WorkOrderRecord(this.LocalPluginContext.PreImage, this.LocalPluginContext.CurrentUserService);

            if (this.LocalPluginContext.PostImage != null)
                this.TargetImage = new WorkOrderRecord(this.LocalPluginContext.TargetEntity, this.LocalPluginContext.CurrentUserService);
        }

        private void callCancelExternalWorkOrderAction(string externalSysName, string externalSysRecordId)
        {
            this.TraceMessage += "|ifm_cancelexternalworkorder: Start|";
            OrganizationRequest orgRequest = new OrganizationRequest("ifm_cancelexternalworkorder");
            orgRequest["WorkOrder"] = new EntityReference(PostImage.LogicalName, PostImage.Id);
            orgRequest["Target"] = new EntityReference(PostImage.LogicalName, PostImage.Id);
            orgRequest["ExternalSystemName"] = externalSysName;
            orgRequest["ExternalSystemRecordId"] = externalSysRecordId;
            OrganizationResponse orgResponse = this.LocalPluginContext.CurrentUserService.Execute(orgRequest);
            this.TraceMessage += "|ifm_cancelexternalworkorder: End|";
        }

        private void loadPreChecks(Entity preImage, Entity postImage, Entity targetImage)
        {

            if (postImage != null)
                this.PostImage = new WorkOrderRecord(postImage, this.LocalPluginContext.CurrentUserService);

            if (preImage != null)
                this.PreImage = new WorkOrderRecord(preImage, this.LocalPluginContext.CurrentUserService);

            if (targetImage != null)
                this.TargetImage = new WorkOrderRecord(targetImage, this.LocalPluginContext.CurrentUserService);
        }
        private EntityReference getServiceCategoryContext(WorkOrderRecord srRecord)
        {
            EntityReference srCategory = null;
            //Note: case of reclassification
            //Ravi Sonal: Post Image would work better here. May expect few bugs here
            //Ravi Sonal: Verify this logic with QA team.
            //Assumption: WO definitely will have Service category. Please confirm?
            if (srRecord.ServicecategoryLevel4 != null)
            {
                srCategory = srRecord.ServicecategoryLevel4;
            }
            else if (srRecord.ServicecategoryLevel3 != null)
            {
                srCategory = srRecord.ServicecategoryLevel3;
            }
            return srCategory;
        }
        private EntityCollection getWOApprovals(Guid srId, Guid woID)
        {
            ServiceRequestRecord.StatusCodeEnum srStatus = ServiceRequestRecord.StatusCodeEnum.Null;
            // Check Pre-Approval
            QueryExpression approvalQuery = new QueryExpression()
            {
                EntityName = "ifm_approval",
                ColumnSet = new ColumnSet("ifm_revisedfirstresponsefailuretime", "ifm_revisedresolvedbyfailuretime"),
                Criteria = new FilterExpression()
                {
                    Conditions =
                    {
                        new ConditionExpression("ifm_servicerequestid", ConditionOperator.Equal ,  srId),
                        new ConditionExpression("ifm_workorderid", ConditionOperator.Equal ,  woID)
                    }
                }
            };

            EntityCollection approvalColl = this.LocalPluginContext.SystemUserService.RetrieveMultiple(approvalQuery);
            return approvalColl;
        }
        private bool CheckPriorityChange()
        {
            bool isPriorityChange = false;


            if (PreImage.Priority == null && TargetImage.Priority != null)
            {
                isPriorityChange = true;
            }
            else if (PreImage.Priority != null && TargetImage.Priority == null)
            {
                isPriorityChange = true;
            }
            else if (PreImage.Priority != null && TargetImage.Priority != null
                            && PreImage.Priority.Id != TargetImage.Priority.Id)
            {
                isPriorityChange = true;
            }


            return isPriorityChange;
        }
        private void CreateLog(string subject, string description, WorkOrderRecord wo)
        {
            LogRecord log = new LogRecord(this.orgService);
            log.Subject = subject;
            log.Description = description;
            log.IsActionRequired = true;
            log.HasResolved = false;
            log.Regarding = new EntityReference(wo.LogicalName, wo.Id);
            log.Create();
        }
        private string getSRSubStatusCode(string WOSubStatusName)
        {
            string srSubStatusCode = null;
            if (WOSubStatusName == WorkOrderSubStatusRecord.SubStatusName_AM
               || WOSubStatusName == (WorkOrderSubStatusRecord.SubStatusName_AM + ".")
               || WOSubStatusName == (WorkOrderSubStatusRecord.SubStatusName_AM + "s"))
            {
                srSubStatusCode = ServiceRequestRecord.SubStatusName_PendingParts;
            }
            if (WOSubStatusName == WorkOrderSubStatusRecord.SubStatusName_ER
                || WOSubStatusName == (WorkOrderSubStatusRecord.SubStatusName_ER + ".")
                || WOSubStatusName == (WorkOrderSubStatusRecord.SubStatusName_ER + "s"))
            {
                srSubStatusCode = ServiceRequestRecord.SubStatusName_PendingVendorScheduling;
            }

            return srSubStatusCode;
        }
        private bool checkMaximoIntegrationExist(IOrganizationService service, Guid workOrderId, string statusName)
        {
            bool isExist = false;
            EntityCollection workOrderStatusUpdateCollection = getWorkOrderStatusUpdate(service, workOrderId, statusName, null);
            if (workOrderStatusUpdateCollection.Entities.Count > 0)
            {
                isExist = true;
            }
            return isExist;
        }
        private EntityCollection getWorkOrderStatusUpdate(IOrganizationService service, Guid workOrderId, string statusName, string[] statusNameArray)
        {
            //Ravi Sonal: Move this to Entity controller Record class
            QueryExpression workOrderStatusUpdateQuery = new QueryExpression()
            {
                EntityName = "ifm_workorderstatusupdate",
                ColumnSet = new ColumnSet("ifm_workorderstatusupdateid"),
                Criteria = new FilterExpression()
                {
                    Conditions =
                        {
                            new ConditionExpression("ifm_workorderid", ConditionOperator.Equal, PreImage.Id)
                        }
                }
            };
            if (!string.IsNullOrEmpty(statusName))
            {
                workOrderStatusUpdateQuery.Criteria.AddCondition("ifm_externalsystemstatus", ConditionOperator.Equal, statusName);
            }
            else if (statusNameArray != null && statusNameArray.Length > 0)
            {
                FilterExpression statusFilter = new FilterExpression()
                {
                    FilterOperator = LogicalOperator.Or
                };
                foreach (string status in statusNameArray)
                {
                    statusFilter.AddCondition("ifm_externalsystemstatus", ConditionOperator.Equal, status);
                }
                workOrderStatusUpdateQuery.Criteria.AddFilter(statusFilter);
            }
            EntityCollection workOrderStatusUpdateCollection = service.RetrieveMultiple(workOrderStatusUpdateQuery);

            return workOrderStatusUpdateCollection;
        }
        private void DeleteIncidentType(Guid workOrderId, EntityReference oldIncidentType, IOrganizationService orgSVC)
        {
            //Ravi Sonal: See if this can be moved to class/Manager class
            //Ravi Sonal: Incomplete code placement. find a proper place for it
            if (oldIncidentType == null)
            {
                return;
            }
            QueryExpression workOrderIncidentQuery = new QueryExpression()
            {
                EntityName = "msdyn_workorderincident",
                ColumnSet = new ColumnSet(),
                Criteria = new FilterExpression()
                {
                    Conditions =
                    {
                        new ConditionExpression("msdyn_incidenttype", ConditionOperator.Equal, oldIncidentType.Id),
                        new ConditionExpression("msdyn_workorder", ConditionOperator.Equal, workOrderId)
                    }
                }
            };
            EntityCollection workOrderIncidentCollection = orgSVC.RetrieveMultiple(workOrderIncidentQuery);
            this.TraceMessage += "|Delete msdyn_workorderincident Count: " + workOrderIncidentCollection.Entities.Count + "|";
            foreach (Entity workOrderIncident in workOrderIncidentCollection.Entities)
            {
                WorkOrderIncidentRecord WoIncidentRecord = new WorkOrderIncidentRecord(workOrderIncident, orgSVC);
                WoIncidentRecord.deleteAllProduct(workOrderId);
                WoIncidentRecord.deleteAllService(workOrderId);
                WoIncidentRecord.deleteAllServiceTask(workOrderId);
                WoIncidentRecord.deleteAllRequirementCharacteristic(workOrderId);
                WoIncidentRecord.Delete();
            }
        }
        private void RecreateIncidentType(Guid workOrderId, EntityReference newIncidentType, IOrganizationService orgSVC)
        {
            if (newIncidentType == null)
                return;
            //Ravi Sonal: See if the can stop fetching the Wo again by service call
            //Ravi Sona: Look for service admin
            WorkOrderRecord woRecord = new WorkOrderRecord(workOrderId, orgService);
            Entity woIncidentType = new Entity("msdyn_workorderincident");
            woIncidentType["msdyn_workorder"] = new EntityReference(woRecord.LogicalName, workOrderId);
            woIncidentType["msdyn_incidenttype"] = newIncidentType;
            woIncidentType["msdyn_isprimary"] = true;
            woIncidentType["ownerid"] = woRecord.Owner;
            Guid recordId = orgSVC.Create(woIncidentType);
            this.TraceMessage += "|New msdyn_workorderincident Guid: " + recordId.ToString() + "|";
        }
        private bool IsContractHaveOnHold(Guid contractId)
        {
            //Ravi Sonal: Find the placement for this string
            bool hasOnHold = false;
            SystemSettingRecord sysSettingsRecord = new SystemSettingRecord(this.LocalPluginContext.CurrentUserService);
            EntityCollection settingsCollection = sysSettingsRecord.getSettingsByName("Contract Has On Hold - Code");
            if (settingsCollection.Entities.Count > 0)
            {
                sysSettingsRecord.Record = settingsCollection.Entities[0];
                ClientContractRecord clientContractRecord = new ClientContractRecord(contractId, this.LocalPluginContext.CurrentUserService);

                if (!string.IsNullOrEmpty(sysSettingsRecord.Value)
                    && clientContractRecord.ContractCode == sysSettingsRecord.Value)
                {
                    hasOnHold = true;
                }
            }
            return hasOnHold;
        }
        private void getAllBookingsbyWorkorder(WorkOrderRecord workOrder)
        {
            //Ravi Sonal: Move this to better place
            //Breakdown this code
            QueryExpression queryExpression = new QueryExpression()
            {
                EntityName = "bookableresourcebooking",
                ColumnSet = new ColumnSet("msdyn_workorder", "ownerid"),
                Criteria = new FilterExpression()
                {
                    Conditions =
                    {
                        new ConditionExpression("msdyn_workorder", ConditionOperator.Equal, workOrder.Id)
                    }
                },
                NoLock = true
            };

            EntityCollection queryExpressionResult = this.LocalPluginContext.SystemUserService.RetrieveMultiple(queryExpression);
            this.TraceMessage += "|Method: getAllBookingsbyWorkorder - Bookable Resource Booking Count - " + queryExpressionResult.Entities.Count + "|";
            foreach (Entity BRB in queryExpressionResult.Entities)
            {
                Guid _OwnerID = Guid.Empty;
                if (BRB.Contains("ownerid"))
                    _OwnerID = ((EntityReference)BRB["ownerid"]).Id;

                if (workOrder.Owner.Id != _OwnerID)
                {
                    Entity brBooking = new Entity()
                    {
                        LogicalName = "bookableresourcebooking",
                        Id = BRB.Id
                    };
                    brBooking["ownerid"] = workOrder.Owner;
                    this.LocalPluginContext.SystemUserService.Update(brBooking);
                }
            }
            this.TraceMessage += "|New Owner:" + workOrder.Owner.Id + "-" + workOrder.Owner.Name + "|";
        }
        private void getAllResourceRequirements(WorkOrderRecord workOrder)
        {
            //Ravi Sonal: Move this to better place
            //Breakdown this code


            Guid _OwnerID = Guid.Empty;

            QueryExpression queryExpression = new QueryExpression()
            {
                EntityName = "msdyn_resourcerequirement",
                ColumnSet = new ColumnSet("msdyn_workorder", "ownerid"),
                Criteria = new FilterExpression()
                {
                    Conditions =
                    {
                        new ConditionExpression("msdyn_workorder", ConditionOperator.Equal, workOrder.Id)
                    }
                },
                NoLock = true
            };
            EntityCollection queryExpressionResult = this.LocalPluginContext.CurrentUserService.RetrieveMultiple(queryExpression);
            this.TraceMessage += "|Method: getAllResourceRequirements - Resource Requirement Count - " + queryExpressionResult.Entities.Count + "|";
            foreach (Entity ResourceRequirement in queryExpressionResult.Entities)
            {
                _OwnerID = ((EntityReference)ResourceRequirement.Attributes["ownerid"]).Id;
                if (workOrder.Owner.Id != _OwnerID)
                {
                    //"getAllResourceRequirements: Differnt Owner than Wo Owner. Proceed to change Resource Requirement owner"
                    Entity ResourceRequirement1 = new Entity()
                    {
                        LogicalName = "msdyn_resourcerequirement",
                        Id = ResourceRequirement.Id
                    };
                    ResourceRequirement1["ownerid"] = new EntityReference(workOrder.Owner.LogicalName, workOrder.Owner.Id);
                    this.LocalPluginContext.CurrentUserService.Update(ResourceRequirement1);
                }
            }
            this.TraceMessage += "|New Owner:" + workOrder.Owner.Id + "-" + workOrder.Owner.Name + "|";
        }
        private string GetTransitionType(ServiceCategoryRecord preImageSC, ServiceCategoryRecord postImageSC)
        {
            string transitionType = string.Empty;
            string preSendToExternalSystem = null;
            string postSendToExternalSystem = null;

            if (preImageSC != null && preImageSC.SendToExternalSystem != ServiceCategoryRecord.SendToExternalSystemEnum.Null)
            {
                preSendToExternalSystem = Enum.GetName(typeof(ServiceCategoryRecord.SendToExternalSystemEnum), (int)preImageSC.SendToExternalSystem);
            }
            if (postImageSC != null && postImageSC.SendToExternalSystem != ServiceCategoryRecord.SendToExternalSystemEnum.Null)
            {
                postSendToExternalSystem = Enum.GetName(typeof(ServiceCategoryRecord.SendToExternalSystemEnum), (int)postImageSC.SendToExternalSystem);
                if(postSendToExternalSystem == Enum.GetName(typeof(ServiceCategoryRecord.SendToExternalSystemEnum), ServiceCategoryRecord.SendToExternalSystemEnum.SMSV1))
                {
                    postSendToExternalSystem = string.Empty;
                }
            }

            string transitiveCode = null;
            if (preImageSC.Id != Guid.Empty && postImageSC.Id != Guid.Empty && preImageSC.Id != postImageSC.Id)
            {
                transitiveCode = getTransitiveServiceCategoryCode();
            }
            if (transitiveCode != null)
            {
                if (string.IsNullOrEmpty(preSendToExternalSystem)
                    && !string.IsNullOrEmpty(postSendToExternalSystem)
                    && !preImageSC.isTransitive(transitiveCode)
                    && !postImageSC.isTransitive(transitiveCode))
                {
                    transitionType = "SoftToHard";
                    this.TraceMessage += "|Transition Type: Soft to Hard|";
                }
                else if (string.IsNullOrEmpty(preSendToExternalSystem)
                    && string.IsNullOrEmpty(postSendToExternalSystem)
                    && !preImageSC.isTransitive(transitiveCode)
                    && !postImageSC.isTransitive(transitiveCode))
                {
                    transitionType = "SoftToSoft";
                    this.TraceMessage += "|Transition Type: Soft to Soft";
                }
                else if (!string.IsNullOrEmpty(preSendToExternalSystem)
                    && !preImageSC.isTransitive(transitiveCode)
                    && postImageSC.isTransitive(transitiveCode))
                {
                    transitionType = "RerouteSR";
                    this.TraceMessage += "|Transition Type: Hard to Transitive|";
                }
                else if (preImageSC.isTransitive(transitiveCode)
                   && !postImageSC.isTransitive(transitiveCode)
                   && string.IsNullOrEmpty(preSendToExternalSystem)
                   && !string.IsNullOrEmpty(postSendToExternalSystem))
                {
                    transitionType = "TransToHard";
                    this.TraceMessage += "|Transition Type: Transtive to Hard";
                }
                else if (preImageSC.isTransitive(transitiveCode)
                    && !postImageSC.isTransitive(transitiveCode)
                    && string.IsNullOrEmpty(preSendToExternalSystem))
                {
                    transitionType = "CancelExternalWO";
                    this.TraceMessage += "|Transition Type: Transtive to Soft";
                }
            }
            return transitionType;
        }

        private string GetSMSReclassTransitionType(EntityReference level1PreImageSCRef, EntityReference level1PostImageSCRef)
        {
            string transitionType = string.Empty;
            string preCategoryType = string.Empty;
            string postCategoryType = string.Empty;
            ServiceCategoryRecord level1PreSC = new ServiceCategoryRecord(level1PreImageSCRef.Id,orgService);
            if(level1PreSC !=  null)
            {
                preCategoryType = level1PreSC.getMasterServiceCategoryName();
                this.TraceMessage += "|PreCategoryType: " + preCategoryType + " |";

            }
            ServiceCategoryRecord level1PostSC = new ServiceCategoryRecord(level1PostImageSCRef.Id, orgService);
            if(level1PostSC != null)
            {
                postCategoryType = level1PostSC.getMasterServiceCategoryName();
                this.TraceMessage += "|PostCategoryType: " + postCategoryType + " |";
            }
            
            if (preCategoryType == ServiceCategoryRecord.SoftFM && postCategoryType == ServiceCategoryRecord.HardFM)
            {
                transitionType = "SoftToHard";
                this.TraceMessage += "|Transition Type: Soft to Hard|";
            }
            else if (preCategoryType == ServiceCategoryRecord.SoftFM && postCategoryType == ServiceCategoryRecord.SoftFM)
            {
                transitionType = "SoftToSoft";
                this.TraceMessage += "|Transition Type: Soft to Soft|";
            }
            return transitionType;
        }

        private string getTransitiveServiceCategoryCode()
        {
            //Ravi Sonal: Move this to system settings manager
            string value = null;
            string key = "Transitive Service Category - Code";

            SystemSettingRecord systemSettingRecord = new SystemSettingRecord(this.orgService);
            EntityCollection systemSettings = systemSettingRecord.getSettingsByName(key);
            if (systemSettings.Entities.Count > 0)
            {
                value = systemSettings.Entities[0]["ifm_value"].ToString();
            }
            return value;
        }

        /*


        //Ravi Sonal: for future story
        const string INPRG = "INPRG";
        const string COMP = "COMP";
        const string CLOSE = "CLOSE";
        const string REJ = "REJ";
        const string CAN = "CAN";
        public const string WMATL = "WMATL";
        public const string EREQ = "EREQ";
        public const string EACCEPT = "EACCEPT";
        public const string EREJECT = "EREJECT";
        const string ACK = "ACK";
        public const string RESPONDED = "RESPONDED";
        const string FCOMP = "FCOMP";
        const string DISP = "DISP";
        const string ADMCOMP = "ADMCOMP";
        const string WAPPR = "WAPPR";
        public const string CAN_CD19 = "CAN-CD19";
        const string TLC = "TLC";











        public void UpdateWorkOrder(ifm_workorderstatusupdate workOrderStatusUpdate, IOrganizationService organizationService, ITracingService tracingService, Guid userId)
        {

            Boolean hasResourceBookingUpdated = false;
            Boolean hasWorkOrderUpdated = false;
            var externalSystemStatus = workOrderStatusUpdate.ifm_externalsystemstatus;

            //Step 1: Check if External System is Valid
            if (externalSystemStatus == INPRG || externalSystemStatus == COMP || externalSystemStatus == CLOSE || externalSystemStatus == REJ || externalSystemStatus == CAN ||
                externalSystemStatus == WMATL || externalSystemStatus == EREQ || externalSystemStatus == EACCEPT || externalSystemStatus == EREJECT || externalSystemStatus == ACK ||
                externalSystemStatus == RESPONDED || externalSystemStatus == FCOMP || externalSystemStatus == DISP ||
                externalSystemStatus == ADMCOMP || externalSystemStatus == WAPPR || externalSystemStatus == CAN_CD19 || externalSystemStatus == TLC)
            {
            }
            else
            {
                return;
            }

            // Step 2: Retrieve WO
            var workOrder = context.msdyn_workorderSet.FirstOrDefault(r => r.Id == workOrderStatusUpdate.ifm_workorderid.Id);
            if (workOrder != null)
            {
            }
            else
            {
                return;
            }

            // Step 3: Booking ResourceStatus
            // Retrieve all related resource bookings status
            var resourceBookingsCollection = organizationService.RetrieveMultiple(new FetchExpression(string.Format(Resources.FetchXML.GetBookableResourceBookings, workOrder.Id)));

            var bookingStatusInProgress = context.BookingStatusSet.Single(r => r.Name == INPROGRESS);
            var bookingStatusCompleted = context.BookingStatusSet.Single(r => r.Name == COMPLETED);
            var bookingStatusCanceled = context.BookingStatusSet.Single(r => r.Name == CANCELED);

            foreach (BookableResourceBooking resourceBooking in resourceBookingsCollection.Entities)
            {
                hasResourceBookingUpdated = false;

                BookableResourceBooking tmpBookableResourceBooking = new BookableResourceBooking();
                tmpBookableResourceBooking.BookableResourceBookingId = resourceBooking.BookableResourceBookingId;


                if (externalSystemStatus == INPRG
                || externalSystemStatus == RESPONDED)
                {
                    if (resourceBooking.BookingStatus.Id != bookingStatusInProgress.Id)
                    {
                        tmpBookableResourceBooking.BookingStatus = new EntityReference(BookingStatus.EntityLogicalName, bookingStatusInProgress.Id);
                        hasResourceBookingUpdated = true;
                    }
                }
                if (externalSystemStatus == COMP
                    || externalSystemStatus == TLC)
                {
                    if (resourceBooking.BookingStatus.Id != bookingStatusCompleted.Id)
                    {
                        tmpBookableResourceBooking.BookingStatus = new EntityReference(BookingStatus.EntityLogicalName, bookingStatusCompleted.Id);
                        hasResourceBookingUpdated = true;
                    }
                }
                if (externalSystemStatus == REJ
                    || externalSystemStatus == CAN
                    || externalSystemStatus == CAN_CD19)
                {
                    if (resourceBooking.BookingStatus.Id != bookingStatusCanceled.Id)
                    {
                        tmpBookableResourceBooking.BookingStatus = new EntityReference(BookingStatus.EntityLogicalName, bookingStatusCanceled.Id);
                        hasResourceBookingUpdated = true;
                    }
                }

                if (tmpBookableResourceBooking != null && tmpBookableResourceBooking.BookingStatus.Id == bookingStatusCompleted.Id)
                {
                    if (getDateTimeForBooking(DateTime.Now) <= getDateTimeForBooking(resourceBooking.msdyn_ActualArrivalTime.Value))
                    {
                        // This logic will get executed only when rapid updates pushed from MAximo to D365 to avoid Field service validation error.
                        tmpBookableResourceBooking.msdyn_ActualArrivalTime = resourceBooking.StartTime.Value;
                        tmpBookableResourceBooking.EndTime = tmpBookableResourceBooking.msdyn_ActualArrivalTime.Value.AddMinutes(1);
                    }
                }
                if (hasResourceBookingUpdated == true)
                {
                    UpdateRequest updateRequest = new UpdateRequest { Target = tmpBookableResourceBooking };
                    executeMultipleRequest.Requests.Add(updateRequest);
                }

            }

            // Step 4:




            if (externalSystemStatus == WMATL
                || externalSystemStatus == EREQ
                || externalSystemStatus == EACCEPT
                || externalSystemStatus == EREJECT)
            {
                hasWorkOrderUpdated = true;
            }
            if (externalSystemStatus == RESPONDED)
            {
                // Update Booking and WO to In-Progress
                if (resourceBooking.BookingStatus.Id == bookingStatusInProgress.Id)
                {
                    hasWorkOrderUpdated = true;
                }
            }


            if (externalSystemStatus == DISP)
            {
                // Update OnHold (WMATL) to InProgress
                this.WorkOrderStatusUpdate(userId, workOrder.Id, 0, externalSystemStatus, workOrder, true);
            }
            if (externalSystemStatus == INPRG)
            {
                if (resourceBooking.BookingStatus.Id == bookingStatusInProgress.Id)
                {
                    // Update OnHold (WMATL) to InProgress
                    this.WorkOrderStatusUpdate(workOrder.Id, 0, externalSystemStatus, workOrder, true);

                }
            }

            if (externalSystemStatus == CLOSE)
            {
                if (resourceBooking.BookingStatus.Id != bookingStatusCompleted.Id)
                {
                    string lcid = UserSettingManager.GetUserLanguage(userId, organizationService);
                    if (!string.IsNullOrEmpty(lcid))
                    {
                        MessageTranslationManager translationManager = new MessageTranslationManager(this.context);
                        // "Resource bookings for work order are not yet completed."
                        throw new InvalidPluginExecutionException(translationManager.GetTranslation(organizationService, tracingService, "WO005", lcid));
                    }
                }
                if (workOrder.msdyn_SystemStatus.Value != closedPosted)
                    this.WorkOrderStatusUpdate(organizationService, tracingService, userId, workOrder.Id, closedPosted, externalSystemStatus, workOrder, true);
            }

            if (externalSystemStatus == FCOMP)
            {
                return;
            }










            if (externalSystemStatus == TLC)
            {
                if (resourceBooking.BookingStatus.Id != bookingStatusCompleted.Id)
                {
                    if (getDateTimeForBooking(DateTime.Now) <= getDateTimeForBooking(resourceBooking.msdyn_ActualArrivalTime.Value))
                    {
                        // This logic will get executed only when rapid updates pushed from MAximo to D365 to avoid Field service validation error.
                        tmpBookableResourceBooking.msdyn_ActualArrivalTime = resourceBooking.StartTime.Value;
                        tmpBookableResourceBooking.EndTime = tmpBookableResourceBooking.msdyn_ActualArrivalTime.Value.AddMinutes(1);
                    }
                    hasResourceBookingUpdated = true;
                }
            }
            if (externalSystemStatus == COMP)
            {
                if (resourceBooking.BookingStatus.Id != bookingStatusCompleted.Id)
                {
                    if (getDateTimeForBooking(DateTime.Now) <= getDateTimeForBooking(resourceBooking.msdyn_ActualArrivalTime.Value))
                    {
                        //This logic will get executed only when rapid updates pushed from MAximo to D365 to avoid Field service validation error.
                        tmpBookableResourceBooking.msdyn_ActualArrivalTime = resourceBooking.StartTime.Value;
                        tmpBookableResourceBooking.EndTime = tmpBookableResourceBooking.msdyn_ActualArrivalTime.Value.AddMinutes(1);
                    }
                    hasResourceBookingUpdated = true;
                }
            }



            if (resourceBookingsCollection.Entities.Count == 0)
            {
                hasWorkOrderUpdated = true;
            }
            if (externalSystemStatus == REJ
                    || externalSystemStatus == CAN
                    || externalSystemStatus == CAN_CD19)
            {
                hasWorkOrderUpdated = true;
            }



            //DISP: Back to In progress from On Hold
            // Step 10: Update WO System Status 
            int systemStatusOption = 0;
            if (hasWorkOrderUpdated == true)
            {
                if (externalSystemStatus == REJ
                    || externalSystemStatus == DISP)
                {
                    systemStatusOption = openUnscheduled;
                }
                else if (externalSystemStatus == CAN
                    || externalSystemStatus == CAN_CD19)
                {
                    systemStatusOption = closedCanceled;
                }
                else if (externalSystemStatus == RESPONDED)
                {
                    systemStatusOption = openInprogress;
                }
                if (externalSystemStatus == WMATL
                || externalSystemStatus == EREQ
                || externalSystemStatus == EACCEPT
                || externalSystemStatus == EREJECT)
                {
                    if (systemStatusOption == openScheduled
                        || systemStatusOption == openInprogress
                        || systemStatusOption == openUnscheduled)
                    {
                        systemStatusOption = workOrder.msdyn_SystemStatus.Value;
                    }
                }
                if (externalSystemStatus == INPRG)
                    systemStatusOption = openUnscheduled;
                if (externalSystemStatus == COMP)
                    systemStatusOption = openCompleted;
                if (externalSystemStatus == CLOSE)
                    systemStatusOption = closedPosted;
                if (externalSystemStatus == CAN)
                    systemStatusOption = closedCanceled;
                if (externalSystemStatus == WMATL)
                    systemStatusOption = openUnscheduled;
                if (externalSystemStatus == EREQ)
                    systemStatusOption = openUnscheduled;
                if (externalSystemStatus == EACCEPT)
                    systemStatusOption = openUnscheduled;
                if (externalSystemStatus == EREJECT)
                    systemStatusOption = openUnscheduled;
                if (externalSystemStatus == RESPONDED)
                    systemStatusOption = openUnscheduled;
            }
            if (systemStatusOption != 0)
            {
                this.WorkOrderStatusUpdate(userId, workOrder.Id, systemStatusOption, externalSystemStatus, workOrder, true);
            }




            // Step 11: 


            if (executeMultipleRequest.Requests.Count > 0)
            {
                ExecuteMultipleResponse responseWithResults = (ExecuteMultipleResponse)organizationService.Execute(executeMultipleRequest);
                foreach (var responseItem in responseWithResults.Responses)
                {
                    // if any error response.
                    if (responseItem.Fault != null)
                    {
                        throw new InvalidPluginExecutionException(OperationStatus.Failed, responseItem.Fault.Message);
                    }
                }
            }
            //Ravi Sonal: See this later
            //On  HOld reason is null
            if (false)
            {

                string lcid = UserSettingManager.GetUserLanguage(userId, organizationService);
                if (!string.IsNullOrEmpty(lcid))
                {
                    MessageTranslationManager translationManager = new MessageTranslationManager(this.context);
                    // context.ifm_systemsettingSet.FirstOrDefault(a => a.ifm_name == ServiceRequestManager.ON_HOLD_REASON).ifm_value;
                    string onHoldReason = translationManager.GetTranslation(organizationService, tracingService, "SR013", lcid);

                    if (onHoldReason == string.Empty)
                    {
                        tracingService.Trace("Create Log for On Hold Reason doesn't exist in Message Translation.");
                        ifm_log log = new ifm_log()
                        {
                            Subject = string.Format("Message Translation - Missing data"),
                            Description = string.Format("Message Translation doesn't have code : {0}", "SR013"),
                            ifm_isactionrequired = true,
                            ifm_hasresolved = false,
                            RegardingObjectId = new EntityReference(msdyn_workorder.EntityLogicalName, workOrderId)
                        };
                        context.AddObject(log);
                        context.SaveChanges();
                        return;
                    }

                }
            }
        }

        private void WorkOrderStatusUpdate(Guid workOrderId, int status, string externalSystemStatus, msdyn_workorder workOrderContext)
        {
            //System Status
            //Status Code
            //Sub Status
            //On Hold Reason


            //Case 1: workOrder.Id, 0, externalSystemStatus, workOrder, true// "INPRG"



            bool isOnHold = false;
            bool isUpdateWorkOrder = false;

            msdyn_workorder workOrder = new msdyn_workorder();
            workOrder.Id = workOrderId;


            //System Status
            if (workOrderContext.msdyn_SystemStatus.Value != status && status != 0)
            {
                workOrder.msdyn_SystemStatus = new OptionSetValue(status);
                isUpdateWorkOrder = true;
            }

            //Status Code
            if (externalSystemStatus == REJ)
            {
                isUpdateWorkOrder = true;
                if (workOrderContext.statecode != msdyn_workorderState.Active)
                {
                    workOrder.statecode = msdyn_workorderState.Active;
                }
                workOrder.statuscode = msdyn_workorder_statuscode.Rejected;
            }

            //Sub Status
            string subStatus = null;
            if (!isOnHold && workOrderContext.msdyn_SubStatus != null)
            {
                if (externalSystemStatus == CAN_CD19)
                {
                    subStatus = SUBSTATUS_COVID19;
                }
                if (externalSystemStatus == WMATL 
                    || externalSystemStatus == EREQ 
                    || externalSystemStatus == EACCEPT 
                    || externalSystemStatus == EREJECT)
                {
                    if(externalSystemStatus == WMATL)
                    {
                        subStatus = SUBSTATUS_AWAITING_MATERIAL;
                    }
                    if (externalSystemStatus == EREQ
                        || externalSystemStatus == EACCEPT
                        || externalSystemStatus == EREJECT)
                    {
                        subStatus = SUBSTATUS_EXTENSION_REQUESTED;
                    }
                    // TODO: Temporary workaround WO Sub statuses
                    if (status == openScheduled)
                    {
                        subStatus = subStatus + ".";
                    }
                    else if (status == openInprogress)
                    {
                        subStatus = subStatus + "s";
                    }
                }
            }
            if (subStatus != null)
            {
                Guid t1 = context.msdyn_workordersubstatusSet.FirstOrDefault(a => a.msdyn_name == subStatus && a.msdyn_SystemStatus.Value == status).Id;
                workOrder.msdyn_SubStatus = new EntityReference(msdyn_workordersubstatus.EntityLogicalName, t1);


                Guid t = context.msdyn_workordersubstatusSet.FirstOrDefault(a => a.Id == new Guid(subStatus)).Id;
                workOrder.msdyn_SubStatus = new EntityReference(msdyn_workordersubstatus.EntityLogicalName, t);
                isUpdateWorkOrder = true;
            }

            isOnHold = true;// Ravi Sonal: Check This one

            if (externalSystemStatus != CAN_CD19)
            {
                workOrder.msdyn_SubStatus = null;

                isUpdateWorkOrder = true;
            }

            //On Hold Reason
            if (!isOnHold && !string.IsNullOrEmpty(workOrderContext.ifm_onholdreason))
            {
                workOrder.ifm_onholdreason = string.Empty;
                isUpdateWorkOrder = true;
            }
            else if (externalSystemStatus == WMATL || externalSystemStatus == EREQ || externalSystemStatus == EACCEPT || externalSystemStatus == EREJECT)
            {
                workOrder.ifm_onholdreason = onHoldRbadeason;
            }




            if (isUpdateWorkOrder)
            {
                organizationService.Update(workOrder);
            }
            else if (workOrderContext.msdyn_ServiceRequest != null)
            {
                // No changes on WO system status: INPRG to RESPONDED
                // Update FirstResponseSent for HardFM only for RESPONDED

                var stateCodeVal = context.IncidentSet.FirstOrDefault(a => a.IncidentId.Value == workOrderContext.msdyn_ServiceRequest.Id).StateCode;

                if (externalSystemStatus == WorkOrderManager.RESPONDED
                    && stateCodeVal == IncidentState.Active)
                {
                    var serviceRequest = new Incident()
                    {
                        Id = workOrderContext.msdyn_ServiceRequest.Id,
                        FirstResponseSent = true
                    };
                    organizationService.Update(serviceRequest);
                }
            }
        }

        private DateTime getDateTimeForBooking(DateTime bookingtime)
        {
            DateTime dateTimePart = new DateTime(bookingtime.Year, bookingtime.Month, bookingtime.Day, bookingtime.Hour, bookingtime.Minute, 0);

            return dateTimePart;

        }
        */
    }
}
