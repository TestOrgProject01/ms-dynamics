using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Helpers;
using Microsoft.Crm;
using Microsoft.Crm.Sdk.Messages;
using Microsoft.Xrm.Sdk;

namespace MGMRI_Sales.Plugins.Activities
{
    public class CreateDeleteLogOnActivityDelete : IPlugin
    {
        public void Execute(IServiceProvider serviceProvider)
        {
            try
            {
                IPluginExecutionContext context = (IPluginExecutionContext)
                    serviceProvider.GetService(typeof(IPluginExecutionContext));
                ITracingService tracingService = (ITracingService)
                    serviceProvider.GetService(typeof(ITracingService));
                IOrganizationServiceFactory serviceFactory = (IOrganizationServiceFactory)
                    serviceProvider.GetService(typeof(IOrganizationServiceFactory));
                IOrganizationService service = (IOrganizationService)
                    serviceFactory.CreateOrganizationService(context.UserId);

                tracingService.LogStart();
                tracingService.LogEntryPoint(context);

                Guid activityId = Guid.Empty;
                EntityReference entActivity = null;
                string cidlOrigin = string.Empty;
                Entity entpreImageActivity = (Entity)context.PreEntityImages["PreImage_Activity"];
                Entity entDeleteLog = null;
                String amadeusIntegrationId = String.Empty;
                //Guid dynamicsRecordId = Guid.Empty;
                string entLogicalName = string.Empty;
                string deleteLogName = string.Empty;

                if (context.IsValidTargetEntityReference())
                {
                    tracingService.LogValidTarget();
                    entActivity = (EntityReference)context.InputParameters["Target"];
                    activityId = entActivity.Id;
                    tracingService.Trace("Activity Id: " + activityId);
                    entLogicalName = entActivity.LogicalName;
                    tracingService.Trace("Logical Name : " + entLogicalName);
                }
                else
                {
                    tracingService.LogInvalidTarget();
                }

                if (
                    entpreImageActivity.Contains("mgm_origin_txt")
                    && entpreImageActivity["mgm_origin_txt"] != null
                )
                {
                    cidlOrigin = entpreImageActivity["mgm_origin_txt"].ToString();
                    tracingService.Trace("Cidl Origin: " + cidlOrigin);
                }

                if (
                    entpreImageActivity.Contains("gc_amadeusintegrationreferenceid")
                    && entpreImageActivity["gc_amadeusintegrationreferenceid"] != null
                )
                {
                    amadeusIntegrationId = (string)
                        entpreImageActivity["gc_amadeusintegrationreferenceid"];
                    tracingService.Trace("Amadeus Id: " + amadeusIntegrationId);
                }

                deleteLogName = GenerateDeleteLogName(entLogicalName);
                tracingService.Trace("Generated DeleteLogName: " + deleteLogName);
                entDeleteLog = new Entity("gc_deletelogs");

                if (deleteLogName != string.Empty)
                {
                    tracingService.Trace($"Setting gc_name to {deleteLogName}");
                    entDeleteLog["gc_name"] = deleteLogName;
                }

                if (amadeusIntegrationId != String.Empty)
                {
                    tracingService.Trace($"Setting gc_amadeusintegrationreferenceid to {amadeusIntegrationId}");
                    entDeleteLog["gc_amadeusintegrationreferenceid"] =
                        amadeusIntegrationId.ToString();
                }

                if (activityId != Guid.Empty)
                {
                    tracingService.Trace($"Setting gc_dynamicsrecordguid to {activityId}");
                    entDeleteLog["gc_dynamicsrecordguid"] = activityId.ToString();
                }

                tracingService.Trace($"Setting mgm_origin_txt to GSO");
                entDeleteLog["mgm_origin_txt"] = "GSO";

                tracingService.Trace($"Creating log to server...");
                service.Create(entDeleteLog);
                tracingService.LogComplete();
                return;
            }
            catch (InvalidPluginExecutionException ex)
            {
                throw new InvalidPluginExecutionException("Error " + ex.Message);
            }
        }

        public string GenerateDeleteLogName(string logicalName)
        {
            string logName = "Name - Delete Log";
            switch (logicalName)
            {
                case "task":
                    logName = logName.Replace("Name", "Task");
                    break;
                case "appointment":
                    logName = logName.Replace("Name", "Appointment");
                    break;
                case "email":
                    logName = logName.Replace("Name", "Email");
                    break;
                case "phonecall":
                    logName = logName.Replace("Name", "Phone Call");
                    break;
                case "gc_amadeusnotes":
                    logName = logName.Replace("Name", "Notes");
                    break;
            }

            return logName;
        }
    }
}
