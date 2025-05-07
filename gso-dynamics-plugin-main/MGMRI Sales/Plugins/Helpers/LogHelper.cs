using Microsoft.Xrm.Sdk;

namespace Helpers
{
    public static class PluginHelpers
    {
        public static string Test = "TESt";

        public static void LogEntryPoint(
            this ITracingService tracingService,
            IPluginExecutionContext context
        )
        {
            int stage = context.Stage;
            string entityName = context.PrimaryEntityName;
            string messageName = context.MessageName;
            tracingService.Trace($"Operation: {messageName}, Stage: {stage}, Entity: {entityName}");

            IPluginExecutionContext parentContext = context.ParentContext;
            if (parentContext != null)
            {
                string parentMessageName = parentContext.MessageName;
                string parentEntityName = parentContext.PrimaryEntityName;
                // Handle the parent context information as needed
                tracingService.Trace(
                    $"Parent Operation: {parentMessageName}, Entity: {parentEntityName}"
                );
            }
            else
            {
                tracingService.Trace("No parent operation triggered this plugin.");
            }
        }

        public static void LogStart(this ITracingService tracingService)
        {
            tracingService.Trace("Starting...");
        }

        public static void LogComplete(this ITracingService tracingService)
        {
            tracingService.Trace("Complete...");
        }

        public static void Log(this ITracingService tracingService, string msg) =>
            tracingService.Trace(msg);

        public static void LogInvalidTarget(this ITracingService tracingService)
        {
            tracingService.Trace("No Valid Target Found.");
        }

        public static void LogUnsupportedMessageType(
            this ITracingService tracingService,
            string messageName
        )
        {
            tracingService.Trace($"Unsupported Message Type: {messageName}");
        }

        public static void LogValidTarget(this ITracingService tracingService)
        {
            tracingService.Trace("Found Valid Target.");
        }

        public static object GetPostImage(
            this IExecutionContext context,
            string postImageName,
            ITracingService tracingService
        )
        {
            if (!context.PostEntityImages.Contains(postImageName))
            {
                tracingService.Trace($"No Post Image found for {postImageName}");
                return null;
            }
            tracingService.Trace($"Post Image found for {postImageName}");
            return (Entity)context.PostEntityImages[postImageName];
        }

        public static object GetPreImage(
            this IExecutionContext context,
            string preImageName,
            ITracingService tracingService
        )
        {
            if (!context.PreEntityImages.Contains(preImageName))
            {
                tracingService.Trace($"No Pre Image found for {preImageName}");
                return null;
            }
            tracingService.Trace($"Pre Image found for {preImageName}");
            return (Entity)context.PreEntityImages[preImageName];
        }

        public static bool IsValidTargetEntity(this IPluginExecutionContext context)
        {
            return context.InputParameters.Contains("Target")
                && context.InputParameters["Target"] is Entity;
        }

        public static bool IsValidTargetEntityReference(this IPluginExecutionContext context)
        {
            return context.InputParameters.Contains("Target")
                && context.InputParameters["Target"] is EntityReference;
        }
    }
}
