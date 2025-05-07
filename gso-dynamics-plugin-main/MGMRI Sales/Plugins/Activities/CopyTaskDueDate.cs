using System;
using Helpers;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;

namespace MGMRI_Sales.Plugins.Activities
{
    public class CopyTaskDueDate : IPlugin
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
                IOrganizationService orgService = (IOrganizationService)
                    serviceFactory.CreateOrganizationService(context.UserId);

                tracingService.LogStart();
                tracingService.LogEntryPoint(context);

                //Entity entPostImageAccount = (Entity)context.PostEntityImages["PostImage_Account"];
                Guid regardingObjectId = Guid.Empty;

                Entity entActivity = null;
                Guid activityId = Guid.Empty;
                EntityReference modifiedByUser = null;
                EntityReference createdByUser = null;
                Guid integrationUser = Guid.Empty;

                string targetEntityLogicalName = string.Empty;

                if (context.Depth > 1)
                {
                    tracingService.Trace("Contact depth is greater than 1");
                    tracingService.LogComplete();
                    return;
                }

                if (!context.IsValidTargetEntity())
                {
                    tracingService.LogInvalidTarget();
                    tracingService.LogComplete();
                    return;
                }

                // Obtain the target entity from the input parameters.
                tracingService.LogValidTarget();
                entActivity = (Entity)context.InputParameters["Target"];
                activityId = entActivity.Id;
                Entity updateTaskEntity = orgService.Retrieve(
                    "task",
                    entActivity.Id,
                    new ColumnSet("modifiedby", "createdby", "scheduledend", "gc_gso_duedate")
                );

                if (updateTaskEntity.Contains("modifiedby"))
                {
                    modifiedByUser = (EntityReference)updateTaskEntity["modifiedby"];
                    tracingService.Trace("Modified By Set to" + modifiedByUser.Id);
                }

                if (updateTaskEntity.Contains("createdby"))
                {
                    createdByUser = (EntityReference)updateTaskEntity["createdby"];
                    tracingService.Trace("Created By Set To" + createdByUser.Id);
                }
                integrationUser = getIntegrationUser(orgService);
                tracingService.Trace("Integration User Id" + integrationUser);

                switch (context.MessageName.ToUpper())
                {
                    case "CREATE":
                        tracingService.Trace("Message Name " + context.MessageName);
                        ProcessMessage_Create(
                            tracingService,
                            orgService,
                            createdByUser,
                            integrationUser,
                            updateTaskEntity
                        );
                        tracingService.LogComplete();
                        return;
                    case "UPDATE":
                        tracingService.Trace("Message Name " + context.MessageName);
                        ProcessMessage_Update(
                            tracingService,
                            orgService,
                            modifiedByUser,
                            integrationUser,
                            updateTaskEntity
                        );
                        tracingService.LogComplete();
                        return;
                    default:
                        tracingService.LogUnsupportedMessageType(context.MessageName);
                        tracingService.LogComplete();
                        return;
                }
            }
            catch (InvalidPluginExecutionException e)
            {
                throw new InvalidPluginExecutionException(e.Message);
            }
        }

        private static bool ProcessMessage_Update(
            ITracingService tracingService,
            IOrganizationService orgService,
            EntityReference modifiedByUser,
            Guid integrationUser,
            Entity updateTaskEntity
        )
        {
            if (integrationUser == Guid.Empty)
            {
                tracingService.Trace("Integration User is empty. No processing needed.");
                tracingService.LogComplete();
                return false;
            }
            if (modifiedByUser == null)
            {
                tracingService.Trace("Modified User is empty. No processing needed.");
                tracingService.LogComplete();
                return false;
            }

            if (modifiedByUser.Id == integrationUser)
            {
                tracingService.Trace("Modified User is integraton user. No processing needed.");
                tracingService.LogComplete();
                return false;
            }
            if (
                !updateTaskEntity.Contains("scheduledend")
                || updateTaskEntity["scheduledend"] == null
            )
            {
                tracingService.Trace(
                    "The scheduledend date value was not set. No processing needed."
                );
                tracingService.LogComplete();
                return false;
            }

            SyncTaskDueDates(tracingService, orgService, updateTaskEntity);
            return true;
        }

        private static bool ProcessMessage_Create(
            ITracingService tracingService,
            IOrganizationService orgService,
            EntityReference createdByUser,
            Guid integrationUser,
            Entity updateTaskEntity
        )
        {
            if (integrationUser == Guid.Empty)
            {
                tracingService.Trace("Integration User is empty. No processing needed.");
                tracingService.LogComplete();
                return false;
            }
            if (createdByUser.Id == integrationUser)
            {
                tracingService.Trace("Created By user is Integration User. No processing needed.");
                tracingService.LogComplete();
                return false;
            }
            if (!updateTaskEntity.Contains("scheduledend"))
            {
                tracingService.Trace(
                    "The scheduledend date value was not set. No processing needed."
                );
                tracingService.LogComplete();
                return false;
            }

            SyncTaskDueDates(tracingService, orgService, updateTaskEntity);
            return true;
        }

        private static DateTime SyncTaskDueDates(
            ITracingService tracingService,
            IOrganizationService orgService,
            Entity updateTaskEntity
        )
        {
            DateTime dtDueDate;
            tracingService.Trace("Setting Due Date to GSO Due Date...");
            dtDueDate = DateTime
                .Parse(updateTaskEntity["scheduledend"].ToString())
                .ToUniversalTime();
            tracingService.Trace("Due Date Value " + dtDueDate);
            updateTaskEntity["gc_gso_duedate"] = dtDueDate;
            tracingService.Trace("Copied Value " + updateTaskEntity["gc_gso_duedate"]);
            orgService.Update(updateTaskEntity);
            return dtDueDate;
        }

        public Guid getIntegrationUser(IOrganizationService service)
        {
            //changed the integration user detailed from # crm-dynamic-sp-uw-d to # crminc-ga-sp-dyn-p-2 100th line
            string fetchXml =
                @"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                          <entity name='systemuser'>
                                            <attribute name='fullname' />
                                            <attribute name='businessunitid' />
                                            <attribute name='systemuserid' />
                                            <order attribute='fullname' descending='false' />
                                            <filter type='and'>
                                              <condition attribute='fullname' operator='eq' value='# crminc-ga-sp-dyn-p-2' /> 
                                              <condition attribute='isdisabled' operator='eq' value='0' />
                                            </filter>
                                          </entity>
                                        </fetch>";
            EntityCollection users = service.RetrieveMultiple(new FetchExpression(fetchXml));
            if (users.Entities.Count > 0)
                return users.Entities[0].Id;

            return Guid.Empty;
        }
    }
}
