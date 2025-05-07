using System;
using System.Collections.Generic;
using System.Linq;
using Helpers;
using Microsoft.Crm.Sdk;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;

namespace MGMRI_Sales.Plugins.Inquiries
{
    public class BuildAccessor : IPlugin
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

                // Log start
                tracingService.LogStart();
                tracingService.LogEntryPoint(context);
                var version = BuildHelper.VersionNumber();
                tracingService.Log($"Current DLL Version: {version}");
                tracingService.LogComplete();
            }
            catch (InvalidPluginExecutionException e)
            {
                throw new InvalidPluginExecutionException(e.Message);
            }
        }

        /* Activites: Phone Call,Email,Appointment,Task */
        public EntityCollection getRelatedActivities(
            IOrganizationService service,
            ITracingService tracing,
            Guid inquiryId
        )
        {
            string fetchXML =
                @"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                              <entity name='activitypointer'>
                                <attribute name='activitytypecode' />
                                <attribute name='subject' />
                                <attribute name='statecode' />
                                <attribute name='modifiedon' />
                                <attribute name='createdby' />
                                <attribute name='activityid' />
                                <order attribute='modifiedon' descending='false' />
                                <link-entity name='lead' from='leadid' to='regardingobjectid' link-type='inner' alias='ag'>
                                  <filter type='and'>
                                    <condition attribute='leadid' operator='eq' uiname='Test Inq12' uitype='lead' value='{"
                + inquiryId
                + @"}' />
                                  </filter>
                                </link-entity>
                              </entity>
                            </fetch>";
            EntityCollection ecActivities = service.RetrieveMultiple(new FetchExpression(fetchXML));
            return ecActivities;
        }

        /*Notes*/
        public EntityCollection getRelatedNotes(
            IOrganizationService service,
            ITracingService tracing,
            Guid inquiryId
        )
        {
            string fetchXML =
                @"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                  <entity name='annotation'>
                                    <attribute name='subject' />
                                    <attribute name='notetext' />
                                    <attribute name='filename' />
                                    <attribute name='annotationid' />
                                    <attribute name='modifiedon' />
                                    <attribute name='modifiedby' />
                                    <attribute name='createdby' />
                                    <order attribute='subject' descending='false' />
                                    <link-entity name='lead' from='leadid' to='objectid' link-type='inner' alias='ad'>
                                      <filter type='and'>
                                        <condition attribute='leadid' operator='eq' uiname='Test Inquiry 001' uitype='lead' value='{"
                + inquiryId
                + @"}' />
                                      </filter>
                                    </link-entity>
                                  </entity>
                                </fetch>";
            EntityCollection ecActivities = service.RetrieveMultiple(new FetchExpression(fetchXML));
            return ecActivities;
        }

        public void updateRelatedActivitiesRPAFlag(
            IOrganizationService service,
            ITracingService tracing,
            Guid inquiryId,
            EntityCollection ecActivities,
            string amadeusIntegrationRefId
        )
        {
            tracing.Trace("Inside update ");
            string activityType;
            string entLogicalName = string.Empty;
            foreach (Entity ec in ecActivities.Entities)
            {
                Entity entActivity = ec;
                tracing.Trace("Activity Id" + entActivity.Id);
                activityType = entActivity["activitytypecode"].ToString();
                tracing.Trace("Activity Type" + activityType);
                EntityReference createdBy = (EntityReference)entActivity["createdby"];
                tracing.Trace("Created By: " + createdBy.Name);
                if (activityType != string.Empty && activityType != null)
                {
                    tracing.Trace("Inside update Activity" + activityType);
                    Entity updateActivity = new Entity(activityType, entActivity.Id);
                    updateActivity["gc_isrpaintegrated"] = true;
                    updateActivity["modifiedby"] = new EntityReference("systemuser", createdBy.Id);
                    service.Update(updateActivity);
                    tracing.Trace(activityType + " Activity updated" + updateActivity.Id);
                }
            }
        }

        public void updateRelatedNotes(
            IOrganizationService service,
            ITracingService tracing,
            Guid inquiryId,
            EntityCollection ecNotes,
            string amadeusIntegrationRefId
        )
        {
            tracing.Trace("Inside update Notes");
            string entLogicalName = string.Empty;
            foreach (Entity ec in ecNotes.Entities)
            {
                Entity entNotes = ec;
                tracing.Trace("Notes Id" + entNotes.Id);
                string strTitle = entNotes["subject"].ToString();
                EntityReference createdBy = (EntityReference)entNotes["createdby"];
                tracing.Trace("Created By: " + createdBy.Name);
                if (strTitle != string.Empty || strTitle != null)
                {
                    tracing.Trace("Inside update Notes subject ");
                    Entity updateNotes = new Entity("annotation", entNotes.Id);
                    updateNotes["subject"] = strTitle + ".";
                    updateNotes["modifiedby"] = new EntityReference("systemuser", createdBy.Id);
                    service.Update(updateNotes);
                    tracing.Trace("Notes Activity updated" + updateNotes.Id);
                }
            }
        }
    }
}
