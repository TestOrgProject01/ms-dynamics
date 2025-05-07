using Microsoft.Crm.Sdk.Messages;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Discovery;
using Microsoft.Xrm.Sdk.Query;
using System;
using System.DirectoryServices;

namespace GSO_Plugins
{
    /// <summary>
    /// Plugin development guide: https://docs.microsoft.com/powerapps/developer/common-data-service/plug-ins
    /// Best practices and guidance: https://docs.microsoft.com/powerapps/developer/common-data-service/best-practices/business-logic/
    /// </summary>
    public class GenerateBookingsUrl : PluginBase
    {
        public GenerateBookingsUrl(string unsecureConfiguration, string secureConfiguration)
            : base(typeof(GenerateBookingsUrl))
        {
            // TODO: Implement your custom configuration handling
            // https://docs.microsoft.com/powerapps/developer/common-data-service/register-plug-in#set-configuration-data
        }

        // Entry point for custom business logic execution
        protected override void ExecuteDataversePlugin(ILocalPluginContext localPluginContext)
        {
            // If null, exit
            if (localPluginContext == null)
            {
                throw new ArgumentNullException(nameof(localPluginContext));
            }

            // Get services
            var tracingService = localPluginContext.TracingService;
            var context = localPluginContext.PluginExecutionContext;

            tracingService.Trace($"Plugin Started...");

            // Get message
            // We only run on create and update.
            var msg = context.MessageName?.ToUpper();

            switch (msg)
            {
                case "CREATE":
                case "UPDATE":
                    // We want to regenerate urls in these cases.
                    GenerateBookingUrl_SingleEntity(localPluginContext, tracingService, context);
                    break;
                case "CREATEMULTIPLE":
                case "UPDATEMULTIPLE":
                    GenerateBookingUrl_Multiple(localPluginContext, tracingService, context);
                    tracingService.Trace($"Received Update Multiple");
                    break;
                case "DELETE":
                case "RETRIEVE":
                case "RETRIEVEMULTIPLE":
                case "ASSIGN":
                case "SETSTATE":
                case "GRANTACCESS":
                case "REVOKEACCESS":
                case "MERGE":
                    // Exit if its unsupported.
                    tracingService.Trace($"Unsupported Message: {msg} Exiting");
                    return;
                default:
                    // Exit if unrecognized.
                    tracingService.Trace($"Unknown Message: {msg} Exiting");
                    return;
            }

        }


        private static void GenerateBookingUrl_SingleEntity(ILocalPluginContext localPluginContext, ITracingService tracingService, IPluginExecutionContext context)
        {

            // Confirm we have an entity
            // If not, exit.
            if (!context.InputParameters.Contains("Target") ||
                !(context.InputParameters["Target"] is Entity))
            {
                tracingService.Trace("Entity not found in context. Exiting..");
                throw new Exception("Entity Target not found in plugin context");
            }


            // Get entity
            Entity entity = (Entity)context.InputParameters["Target"];

            EnsureBookingEntity(tracingService, entity.LogicalName);

            // Get fields
            IOrganizationService orgServ = localPluginContext.PluginUserService;
            string orgUrl = GetAmadeusUrl(orgServ, tracingService);
            GenerateBookingsUrlFor(entity, orgUrl, tracingService);

            // Complete
            tracingService.Trace($"Plugin Complete. Exiting...");
        }


        private static void GenerateBookingUrl_Multiple(ILocalPluginContext localPluginContext, ITracingService tracingService, IPluginExecutionContext context)
        {


            // If it is a bulk update.
            if (!context.InputParameters.Contains("Targets"))
            {
                tracingService.Trace("EntityCollection not found in context. Exiting..");
                throw new Exception("EntityCollection Targets not found in plugin context");
            }

            // Get the entityList
            EntityCollection entities = (EntityCollection)context.InputParameters["Targets"];

            // Confirm if we are on a booking
            // If not, exit
            EnsureBookingEntity(tracingService, entities.EntityName);

            // Get fields
            IOrganizationService orgServ = localPluginContext.PluginUserService;
            string orgUrl = GetAmadeusUrl(orgServ, tracingService);

            foreach (var entity in entities.Entities)
            {
                GenerateBookingsUrlFor(entity, orgUrl, tracingService);
            }


            tracingService.Trace($"Plugin Complete. Exiting...");
        }

        public static void GenerateBookingsUrlFor(Entity entity, string orgUrl, ITracingService tracingService)
        {
            string refId = string.Empty;
            string finalUrl = string.Empty;
            // If we have an ref Id, we can build the url.
            // If it doesnt, then we leave it blank
            if (entity.Attributes.Contains("gc_amadeusintegrationreferenceid"))
            {
                refId = entity.GetAttributeValue<string>("gc_amadeusintegrationreferenceid");

                // If Ref Id came back blank, then no need to build.
                if (!string.IsNullOrWhiteSpace(refId))
                {
                    // Build final url
                    finalUrl = orgUrl + refId;
                }
            }

            tracingService.Trace($"Found Amadeus Reference Id {refId} ");
            tracingService.Trace($"Amadeus Url set to {finalUrl} ");

            // Store built value.
            entity["gc_amadeusbooking"] = finalUrl;
        }

        public static string GetAmadeusUrl(IOrganizationService orgServ, ITracingService tracingService)
        {
            // Determine amadeus url.
            string orgUrl = GetEnvironmentVariableValue(tracingService, orgServ, "gc_amadeusbookingUrl");
            tracingService.Trace($"Env Url {orgUrl}");
            return orgUrl;
        }

        public static void EnsureBookingEntity(ITracingService tracingService, string logicalName)
        {
            switch (logicalName.ToLower())
            {
                case "gc_booking":
                    break;
                default:
                    tracingService.Trace($"Found unsupported entity{logicalName} ");
                    return;
            }
        }
        public static string GetEnvironmentVariableValue(ITracingService tracingService, IOrganizationService service, string schemaName)
        {
            // Retrieve environment variable definition
            QueryExpression queryDefinition = new QueryExpression("environmentvariabledefinition")
            {
                ColumnSet = new ColumnSet("schemaname", "defaultvalue")
            };
            queryDefinition.Criteria.AddCondition("schemaname", ConditionOperator.Equal, schemaName);

            EntityCollection definitionResults = service.RetrieveMultiple(queryDefinition);
            if (definitionResults.Entities.Count > 0)
            {
                Entity definition = definitionResults.Entities[0];
                string defaultValue = definition.GetAttributeValue<string>("defaultvalue");

                // Retrieve environment variable value
                QueryExpression queryValue = new QueryExpression("environmentvariablevalue")
                {
                    ColumnSet = new ColumnSet("value")
                };
                queryValue.Criteria.AddCondition("environmentvariabledefinitionid", ConditionOperator.Equal, definition.Id);

                EntityCollection valueResults = service.RetrieveMultiple(queryValue);
                string currentValue = valueResults.Entities.Count > 0 ? valueResults.Entities[0].GetAttributeValue<string>("value") : defaultValue;

                tracingService.Trace($"PluginHelper: Environment variable '{schemaName}' value retrieved: {currentValue}");
                return currentValue;
            }
            else
            {
                tracingService.Trace($"PluginHelper: Environment variable definition '{schemaName}' not found.");
                return null;
            }
        }
    }
}
