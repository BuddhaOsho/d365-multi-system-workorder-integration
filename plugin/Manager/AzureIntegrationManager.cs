using System;
using System.Linq;
using Sodexo.iFM.Shared.EntityController;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Metadata;
using Microsoft.Xrm.Sdk.Query;

namespace Sodexo.iFM.Plugins.Manager
{
    public class AzureIntegrationManager : ManagerBase
    {
        public IOrganizationService orgService;
        public WorkOrderRecord regardingImageWO;
        public WorkOrderRecord regardingTargetWO;
        public WorkOrderRecord regardingPreImageWO;
        public const string AWAITING_PO = "Awaiting PO";
        public const string PO_REQUIRED = "PO_Required";
        public const string QUOTE = "QUOTE";

        public AzureIntegrationManager(ILocalPluginContext localPluginContext)
            : base(localPluginContext)
        {
            orgService = this.LocalPluginContext.CurrentUserService;
        }
        public void LoadRegardingRecord(Entity recordImage, Entity recordTarget, Entity recordPreImage)
        {
            TraceMessage += "|MethodLoadRegardingRecord- WorkOrderId: " + recordImage.Id + "|";
            if (recordImage.LogicalName == WorkOrderRecord.logicalName)
            {
                regardingImageWO = new WorkOrderRecord(recordImage, orgService);
                regardingTargetWO = new WorkOrderRecord(recordTarget, orgService);
                regardingPreImageWO = new WorkOrderRecord(recordPreImage, orgService);

                SetIntegrationFlag(regardingImageWO, regardingTargetWO, regardingPreImageWO);
            }
        }
        public void SetIntegrationFlag(WorkOrderRecord workOrderImage, WorkOrderRecord workOrderTarget, WorkOrderRecord workOrderPreImage)
        {
            try
            {
                TraceMessage += "|Method: WorkOrderAzureIntergration.SetIntegrationFlag|";
                this.LocalPluginContext.Trace(TraceMessage);
                // For Work Order Type = Predictive Maintenance which does not have any Service Category
                if (workOrderImage.ServicecategoryLevel1 == null)
                    return;
                Entity WorkOrderToUpdate = new Entity(workOrderImage.LogicalName);
                WorkOrderToUpdate.Id = workOrderImage.Id;



                //Context 1: Work Order Type
                EntityReference WorkOrderType = workOrderTarget.WorkOrderType != null ? workOrderTarget.WorkOrderType : workOrderImage.WorkOrderType;
                Entity workOrderType = orgService.Retrieve(WorkOrderTypeRecord.logicalName, workOrderImage.WorkOrderType.Id, new ColumnSet("msdyn_name"));
                string woType = string.Empty;
                if (workOrderType.Contains("msdyn_name"))
                {
                    woType = workOrderType.Attributes["msdyn_name"].ToString().ToUpper();
                }
                TraceMessage += "|woType: " + woType + "|";

                //Context 2: Service category Post Image
                //Ravi Sonal: Think of a better way
                EntityReference serviceCategoryPostRef = null;
                if (workOrderImage.ServicecategoryLevel4 != null)
                {
                    serviceCategoryPostRef = workOrderImage.ServicecategoryLevel4;
                }
                else if (workOrderImage.ServicecategoryLevel3 != null)
                {
                    serviceCategoryPostRef = workOrderImage.ServicecategoryLevel3;
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

                //Context 3: Service category Pre Image
                //Just for tracing i have added this code. In Future we will use it and replace other SC reference from this code
                EntityReference serviceCategoryPreRef = null;
                if (workOrderPreImage.ServicecategoryLevel4 != null)
                {
                    serviceCategoryPreRef = workOrderPreImage.ServicecategoryLevel4;
                }
                else if (workOrderPreImage.ServicecategoryLevel3 != null)
                {
                    serviceCategoryPreRef = workOrderPreImage.ServicecategoryLevel3;
                }

                ServiceCategoryRecord srCategoryPreImage = null;
                if (serviceCategoryPreRef != null && serviceCategoryPreRef.Id != null)
                {
                    srCategoryPreImage = new ServiceCategoryRecord(serviceCategoryPreRef.Id, this.orgService);
                    this.TraceMessage += "|Pre Service Category:" + serviceCategoryPreRef.Id + "|";
                }
                else
                {
                    this.TraceMessage += "|Pre Service Category: NULL |";
                }


                //Context 4: External System
                int externalSystemPost = -1;
                if (srCategoryPostImage != null
                    && srCategoryPostImage.SendToExternalSystem != ServiceCategoryRecord.SendToExternalSystemEnum.Null)
                {
                    externalSystemPost = (int)srCategoryPostImage.SendToExternalSystem;
                }
                //Context 5: Is Integrated
                bool isIntegrated = workOrderImage.IsIntegrated;
                TraceMessage += " |Integration Flag: " + isIntegrated + "|";


                if (srCategoryPostImage != null)
                {
                    //var serviceCategoryId = workOrderImage.ServicecategoryLevel4 != null ? workOrderImage.ServicecategoryLevel4.Id : workOrderImage.ServicecategoryLevel3.Id;
                    //var serviceCategory = orgService.Retrieve(ServiceCategoryRecord.logicalName, serviceCategoryId, new ColumnSet("ifm_externalsystemlist"));


                    //set the value of all mandatory fields from target entity if not then from image entity
                    var Priority = workOrderTarget.Priority != null ? workOrderTarget.Priority : workOrderImage.Priority;

                    var SiteId = workOrderTarget.SiteId != null ? workOrderTarget.SiteId : workOrderImage.SiteId;
                    var ServiceRequest = workOrderTarget.ServiceRequest != null ? workOrderTarget.ServiceRequest : workOrderImage.ServiceRequest;
                    var WorkLocation = workOrderTarget.WorkLocation != null ? workOrderTarget.WorkLocation : workOrderImage.WorkLocation;
                    var Name = workOrderTarget.Name != null ? workOrderTarget.Name : workOrderImage.Name;
                    var ServicecategoryLevel3 = workOrderTarget.ServicecategoryLevel3 != null ? workOrderTarget.ServicecategoryLevel3 : workOrderImage.ServicecategoryLevel3;

                    //Check if the values are not null for mandatory fields
                    bool isValid = Priority != null;
                    isValid &= WorkOrderType != null;
                    isValid &= SiteId != null;
                    isValid &= ServiceRequest != null;
                    isValid &= WorkLocation != null;
                    isValid &= Name != null;
                    isValid &= ServicecategoryLevel3 != null;
                    TraceMessage += "|isValid: " + isValid + "|";

                    TraceMessage += " |srCategoryPostImage.SendToExternalSystem: " + (int)srCategoryPostImage.SendToExternalSystem + "|";
                    if ((int)srCategoryPostImage.SendToExternalSystem != -1
                        && srCategoryPostImage.SendToExternalSystem != ServiceCategoryRecord.SendToExternalSystemEnum.Null)
                    {
                        if (!isIntegrated)
                        {
                            //Create log and notify user to fill the mandatory fields
                            string errorMessage = string.Empty;
                            if (Priority == null)
                                errorMessage += "Priority\n";
                            if (WorkOrderType == null)
                                errorMessage += "Work Order Type\n";
                            if (SiteId == null)
                                errorMessage += "Site\n";
                            if (ServiceRequest == null)
                                errorMessage += "Service Request\n";
                            if (WorkLocation == null)
                                errorMessage += "Work Location\n";
                            if (Name == null)
                                errorMessage += "Work Order Number\n";
                            if (ServicecategoryLevel3 == null)
                                errorMessage += "Service Category Level 3\n";

                            if (!isValid && errorMessage != string.Empty)
                            {
                                string errorDetail = "Workorder integration is missing below mandatory information.\n" +
                                                     errorMessage;
                                var subject = Name + " - " + "Missing data";
                                TraceMessage += "|Create Logs|";
                                CreateLog(subject, errorDetail, workOrderImage);
                            }
                        }
                        if (!isIntegrated && isValid)
                        {
                            TraceMessage += "|Case 1 to Set Integrateion: True|";
                            WorkOrderToUpdate.Attributes["ifm_isintegrated"] = true;
                            WorkOrderToUpdate.Attributes["ifm_hasmissingintegrationdata"] = false;
                            TraceMessage += "|OptionSet: " + externalSystemPost + "|";
                            //US 49087 SMS Change by Mubeen, update the external system name on work order
                            WorkOrderToUpdate.Attributes["ifm_externalsystemname"] = srCategoryPostImage.SendToExternalSystem.ToString();
                        }
                        // This is because regardless of Send to External system is blank or value set then system to send record to Maximo.                                      
                        if (woType == QUOTE && workOrderImage.msdyn_systemstatus != null
                            && workOrderImage.msdyn_systemstatus.Value == (int)WorkOrderRecord.SystemStatusEnum.OpenUnscheduled)
                        {
                            TraceMessage += "|workOrderImage.msdyn_systemstatus: OpenUnscheduled|";
                            //SetPoWorkOrderFields(orgService, workOrderImage, WorkOrderToUpdate);
                            // 25/11/2019. Only 'Hard FM' create record in Maximo, but still set WO sub status to Awaiting PO (unlocked)
                            if (workOrderImage.IsIntegrated != true)
                            {
                                WorkOrderToUpdate.Attributes["ifm_isintegrated"] = true;
                                //US 49087 SMS Change by Mubeen, update the external system name on work order
                                WorkOrderToUpdate.Attributes["ifm_externalsystemname"] = srCategoryPostImage.SendToExternalSystem.ToString();
                            }
                            if (workOrderImage.HasMissingIntegrationData != false)
                            {
                                WorkOrderToUpdate.Attributes["ifm_hasmissingintegrationdata"] = false;
                            }
                            // On WO create - set substatus to 'Awaiting PO'
                            //  User Story 16923 - Remove sending Soft Service PO Requests to Maximo
                            if (workOrderImage.SubStatus == null && workOrderImage.PurchaseOrderNumber == null)
                            {
                                WorkOrderSubStatusRecord workOrderSubStatus = new WorkOrderSubStatusRecord(orgService);
                                workOrderSubStatus.getRecordByName(AWAITING_PO);

                                if (workOrderSubStatus != null)
                                {
                                    WorkOrderToUpdate.Attributes["msdyn_substatus"] = workOrderSubStatus.ToEntityReference();
                                }
                            }
                        }
                        else if (woType == QUOTE && workOrderImage.msdyn_systemstatus != null
                                 && workOrderImage.msdyn_systemstatus.Value == (int)WorkOrderRecord.SystemStatusEnum.OpenCompleted)
                        {
                            TraceMessage += "|workOrderImage.msdyn_systemstatus: OpenCompleted|";
                            // Set this field once the Quote Workorder is completed from Maximo and there wont be any integration.
                            WorkOrderToUpdate.Attributes["ifm_isintegrated"] = false;
                        }
                        else if (woType == QUOTE && workOrderImage.msdyn_systemstatus != null
                                 && workOrderImage.msdyn_systemstatus.Value == (int)WorkOrderRecord.SystemStatusEnum.ClosedPosted)
                        {
                            TraceMessage += "|workOrderImage.msdyn_systemstatus: ClosedPosted|";
                            // Set this field once the Quote Workorder is completed from Maximo and there wont be any integration.
                            WorkOrderToUpdate.Attributes["ifm_isintegrated"] = false;
                        }

                    }
                    else if (LocalPluginContext.PluginExecutionContext.MessageName == "Update" &&
                        workOrderImage.ServicecategoryLevel3 != null && workOrderPreImage.ServicecategoryLevel3 != null &&
                        workOrderImage.ServicecategoryLevel3 != workOrderPreImage.ServicecategoryLevel3)
                    {
                        TraceMessage += " |srCategoryPreImage.SendToExternalSystem: " + (int)srCategoryPreImage.SendToExternalSystem + "|";
                        //var serviceCategoryPreId = workOrderPreImage.ServicecategoryLevel4 != null ? workOrderPreImage.ServicecategoryLevel4.Id : workOrderPreImage.ServicecategoryLevel3.Id;
                        //var serviceCategoryPre = orgService.Retrieve(ServiceCategoryRecord.logicalName, serviceCategoryPreId, new ColumnSet("ifm_externalsystemlist"));
                        if ((int)srCategoryPreImage.SendToExternalSystem != -1
                        && srCategoryPreImage.SendToExternalSystem != ServiceCategoryRecord.SendToExternalSystemEnum.Null)
                        {
                            if (isIntegrated && isValid)
                            {
                                TraceMessage += "|Set ifm_isintegrated = False|";
                                WorkOrderToUpdate.Attributes["ifm_isintegrated"] = false;
                            }
                        }
                    }
                }

                if (woType == QUOTE
                    && workOrderImage.PurchaseOrderNumber == null)
                {
                    // User Story 16923 - Remove sending Soft Service PO Requests to Maximo
                    //SetPoWorkOrderFields(orgService, workOrderTarget, WorkOrderToUpdate);
                    TraceMessage += "|Case 2 to Set Integrateion: True|";
                    if (workOrderImage.IsIntegrated != true)
                    {
                        WorkOrderToUpdate.Attributes["ifm_isintegrated"] = true;
                        //US 49087 SMS Change by Mubeen, update the external system name on work order
                        WorkOrderToUpdate.Attributes["ifm_externalsystemname"] = srCategoryPostImage.SendToExternalSystem.ToString();
                    }
                    if (workOrderImage.HasMissingIntegrationData != false)
                    {
                        WorkOrderToUpdate.Attributes["ifm_hasmissingintegrationdata"] = false;
                    }
                    // On WO create - set substatus to 'Awaiting PO'
                    // User Story 16923 - Remove sending Soft Service PO Requests to Maximo
                    if (workOrderImage.SubStatus == null && workOrderImage.PurchaseOrderNumber == null)
                    {
                        WorkOrderSubStatusRecord workOrderSubStatus = new WorkOrderSubStatusRecord(orgService);
                        workOrderSubStatus.getRecordByName(AWAITING_PO);

                        if (workOrderSubStatus != null)
                        {
                            WorkOrderToUpdate.Attributes["msdyn_substatus"] = workOrderSubStatus.ToEntityReference();
                            TraceMessage += "|msdyn_substatus|" + workOrderSubStatus.ToEntityReference().ToString() + "|";
                        }
                    }
                }
                if (WorkOrderToUpdate.Attributes.Count >= 1)
                {
                    orgService.Update(WorkOrderToUpdate);
                }
                this.LocalPluginContext.Trace(TraceMessage);
            }
            catch (Exception ex)
            {
                LocalPluginContext.Trace(TraceMessage + "|" + ex.Message);
                throw new InvalidPluginExecutionException(ex.Message, ex);
            }
        }
        public void CreateLog(string subject, string description, WorkOrderRecord wo)
        {
            LogRecord log = new LogRecord(this.orgService);
            log.Subject = subject;
            log.Description = description;
            log.IsActionRequired = true;
            log.HasResolved = false;
            log.Regarding = new EntityReference(wo.LogicalName, wo.Id);
            log.Create();
        }
    }
}
