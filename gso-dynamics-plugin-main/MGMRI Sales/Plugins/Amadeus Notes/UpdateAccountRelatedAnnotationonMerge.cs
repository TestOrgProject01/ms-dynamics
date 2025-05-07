using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Remoting.Services;
using System.Text;
using System.Threading.Tasks;
using Helpers;
using Microsoft.Crm.Sdk;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;

namespace MGMRI_Sales.Plugins.Amadeus_Notes
{
    public class UpdateAccountRelatedAnnotationonMerge : IPlugin
    {
        public void Execute(IServiceProvider serviceProvider)
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

            Entity preImageEntity =
                context.GetPreImage("preImageAmadeusNotes", tracingService) as Entity;

            if (preImageEntity == null)
            {
                tracingService.Trace("No Pre Image... No processing needed");
                tracingService.LogComplete();
                return;
            }
            Entity postImageEntity =
                context.GetPostImage("postImageAmadeusNotes", tracingService) as Entity;

            // If we have no Post Image, there's no processing needed.
            if (
                postImageEntity == null
                || !postImageEntity.Contains("gc_regardingid")
                || postImageEntity["gc_regardingid"] == null
            )
            {
                tracingService.Trace(
                    "No Post Image or PostImage regarding entity is null... No processing needed"
                );
                tracingService.LogComplete();
                return;
            }

            EntityReference regardingEntityPre = null;
            EntityReference regardingEntityPost = null;
            EntityCollection crmNotes = null;

            tracingService.Trace("Processing Post Image...");
            regardingEntityPost = postImageEntity["gc_regardingid"] as EntityReference;
            tracingService.Trace("PostImage Regarding Id" + regardingEntityPost?.Id ?? "NULL");
            tracingService.Trace(
                "Logical Name Post: " + regardingEntityPost?.LogicalName ?? "NULL"
            );

            if (preImageEntity != null && preImageEntity.Contains("gc_regardingid"))
            {
                tracingService.Trace("Processing Pre Image...");
                regardingEntityPre = preImageEntity["gc_regardingid"] as EntityReference;
                tracingService.Trace("PreImage Regarding Id" + regardingEntityPre?.Id ?? "NULL");
                tracingService.Trace(
                    "Logical Name Pre: " + regardingEntityPre?.LogicalName ?? "NULL"
                );
            }

            tracingService.Trace("Logical Name Pre Out: " + regardingEntityPre?.Id);
            tracingService.Trace("Logical Name Post Out: " + regardingEntityPost?.Id);


            if (regardingEntityPre?.Id == regardingEntityPost?.Id)
            {
                tracingService.Trace("RegardingEntities was not changed. No processing needed... ");
                tracingService.LogComplete();
                return;
            }

            if (
                regardingEntityPost?.LogicalName != "account"
                && regardingEntityPost?.LogicalName != "contact"
            )
            {
                tracingService.Trace(
                    "PostImage Regading Entity was not account or contact. No processing needed... "
                );
                tracingService.LogComplete();
                return;
            }

            tracingService.Trace("Processing changes... ");

            crmNotes = getRelatedAnnotations(
                orgService,
                tracingService,
                regardingEntityPost.LogicalName,
                regardingEntityPre.Id
            );

            tracingService.Trace("Count : " + crmNotes?.Entities?.Count ?? "NULL");

            if (crmNotes?.Entities?.Count <= 0)
            {
                tracingService.Trace("No notes found. No processing needed...");
                tracingService.LogComplete();
                return;
            }

            foreach (Entity entNote in crmNotes.Entities)
            {
                tracingService.Trace("Notes Id" + entNote.Id);
                Entity updateNotes = new Entity("annotation", entNote.Id);
                updateNotes["objectid"] = new EntityReference(
                    regardingEntityPost.LogicalName,
                    regardingEntityPost.Id
                );
                updateNotes["objecttypecode"] = regardingEntityPost.LogicalName;
                orgService.Update(updateNotes);
                tracingService.Trace("Notes Activity updated" + updateNotes);
            }

            tracingService.LogComplete();
        }

        public EntityCollection getRelatedAnnotations(
            IOrganizationService service,
            ITracingService tracing,
            string type,
            Guid regardingpreId
        )
        {
            tracing.Trace("Getting Related notes...");
            string fetchXML =
                @"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                      <entity name='annotation'>
                        <attribute name='subject' />
                        <attribute name='notetext' />
                        <attribute name='filename' />
                        <attribute name='annotationid' />
                        <order attribute='subject' descending='false' />";
            if (type == "account")
                fetchXML +=
                    @"<link-entity name='account' from='accountid' to='objectid' link-type='inner' alias='ae'>
                              <attribute name='parentaccountid' />
                              <filter type='and'>
                                <condition attribute='accountid' operator='eq' uitype='account' value='"
                    + regardingpreId
                    + @"' />
                              </filter>
                            </link-entity>";
            else if (type == "contact")
                fetchXML +=
                    @"<link-entity name='contact' from='contactid' to='objectid' link-type='inner' alias='ai'>
                  <filter type='and'>
                    <condition attribute='contactid' operator='eq' uitype='contact' value='"
                    + regardingpreId
                    + @"' />
                  </filter>
                </link-entity>";
            fetchXML += "</entity></fetch>";
            tracing.Trace("Fetch XML " + fetchXML);
            EntityCollection crmNotes = service.RetrieveMultiple(new FetchExpression(fetchXML));
            return crmNotes;
        }
    }
}
