using System;
using Helpers;
using Microsoft.Xrm.Sdk;

namespace MGMRI_Sales.Plugins.Activities
{
    // Deprecated??
    public class EmailRegardingFlagUpdateOnRelatedToChanges : IPlugin
    {
        public void Execute(IServiceProvider serviceProvider)
        {
            // Obtain the tracing service
            ITracingService tracingService = (ITracingService)
                serviceProvider.GetService(typeof(ITracingService));

            // Obtain the execution context from the service provider
            IPluginExecutionContext context = (IPluginExecutionContext)
                serviceProvider.GetService(typeof(IPluginExecutionContext));

            tracingService.LogStart();
            tracingService.LogEntryPoint(context);

            // Ensure the context is for the email entity and the message is an update
            if (context.PrimaryEntityName != "email")
            {
                tracingService.Log(
                    $"Primary Entity was: {context.PrimaryEntityName}. Expected: 'email'"
                );
                tracingService.Log($"No further processing needed");
                tracingService.LogComplete();
                return;
            }

            if (context.MessageName != "Update")
            {
                tracingService.Log(
                    $"Context Message Name was: {context.MessageName}. Expected: 'Update'"
                );
                tracingService.Log($"No further processing needed");
                tracingService.LogComplete();
                return;
            }

            // The InputParameters collection contains all the data passed in the message request
            if (!context.IsValidTargetEntity())
            {
                tracingService.LogInvalidTarget();
                tracingService.LogComplete();
                return;
            }
            Entity entity = context.InputParameters["Target"] as Entity;

            // Check if the regardingobjectid field is being updated
            if (!entity.Contains("regardingobjectid"))
            {
                tracingService.Log($"RegardingObjectId not changed. No further processing needed");
                tracingService.LogComplete();
                return;
            }

            // Obtain the pre-image from the context
            var preImage = (Entity)context.GetPreImage("PreImage", tracingService);
            if (preImage == null)
            {
                tracingService.Log($"No Pre Image found. No further processing needed");
                tracingService.LogComplete();
                return;
            }

            // Retrieve the old and new values of regardingobjectid
            EntityReference oldRelatedTo = preImage.Contains("regardingobjectid")
                ? (EntityReference)preImage["regardingobjectid"]
                : null;
            EntityReference newRelatedTo = entity.Contains("regardingobjectid")
                ? (EntityReference)entity["regardingobjectid"]
                : null;

            // Check if the regardingobjectid field has changed
            if (
                (oldRelatedTo == null && newRelatedTo != null)
                || (oldRelatedTo != null && !oldRelatedTo.Equals(newRelatedTo))
            )
            {
                tracingService.Log($"RelatedTo object was changed.Processing change...");
                // Obtain the organization service reference
                IOrganizationServiceFactory serviceFactory = (IOrganizationServiceFactory)
                    serviceProvider.GetService(typeof(IOrganizationServiceFactory));
                IOrganizationService service = serviceFactory.CreateOrganizationService(
                    context.UserId
                );

                // Create an entity object for the update
                Entity updateEntity = new Entity(entity.LogicalName, entity.Id);
                //  updateEntity["gc_relatedtoupdateflag"] = true;

                // Update the entity
                tracingService.Log(
                    $"WARNING: Updating an entity with no changes due to requirements change. This plugin should not be triggered..."
                );
                service.Update(updateEntity);

                tracingService.LogComplete();
                return;
            }
            tracingService.Log($"No Processing needed.");
            tracingService.LogComplete();
        }
    }
}
