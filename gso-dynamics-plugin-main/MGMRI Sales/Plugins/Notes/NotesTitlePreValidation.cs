using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Remoting.Services;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Crm.Sdk;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;

namespace MGMRI_Sales.Plugins.Notes
{
    public class NotesTitlePreValidation : IPlugin
    {
        public void Execute(IServiceProvider serviceProvider)
        {
            ITracingService tracingService = (ITracingService)
                serviceProvider.GetService(typeof(ITracingService));
            /* try
             {
                 // Obtain the execution context from the service provider.
                 IPluginExecutionContext context = (IPluginExecutionContext)serviceProvider.GetService(typeof(IPluginExecutionContext));
                 // ITracingService tracingService = (ITracingService)serviceProvider.GetService(typeof(ITracingService));
                 IOrganizationServiceFactory serviceFactory = (IOrganizationServiceFactory)serviceProvider.GetService(typeof(IOrganizationServiceFactory));
                 IOrganizationService orgService = (IOrganizationService)serviceFactory.CreateOrganizationService(context.UserId);
                 // Check if the context is for creating or updating an entity
                 tracingService.Trace(" NotesTitlePreValidation:- start ");
                 if (context.MessageName.ToLower() == "create" || context.MessageName.ToLower() == "update")
                 {
                     tracingService.Trace(" NotesTitlePreValidation:- inside if ");
                     // Check if the target entity is the one you want to validate
                     if (context.InputParameters.Contains("Target") && context.InputParameters["Target"] is Entity entity)
                     {
                         tracingService.Trace(" NotesTitlePreValidation:- inside 2nd if ");
                         // Check if the title attribute exists
                         if (entity.Attributes.Contains("subject"))
                         {
                             tracingService.Trace(" NotesTitlePreValidation:- inside 3rd if ");
                             string subject = entity["subject"] as string;
 
                             // Validate the title length
                             if (subject.Length > 80)
                             {
                                 tracingService.Trace(" NotesTitlePreValidation:- The title cannot exceed 80 characters. ");
                                 string errorMessage = "The title cannot exceed 80 characters. Please shorten the title and try again.";
                                 context.OutputParameters["ValidationMessage"] = errorMessage;
                                 // Throw the exception with the user-friendly message
                                 throw new InvalidPluginExecutionException(errorMessage);
 
                             }
                         }
 
                     }
                 }
             }
             catch (InvalidPluginExecutionException e)
             {
                 tracingService.Trace("NotesTitlePreValidation: Error - " + e.Message);
                 throw new InvalidPluginExecutionException(e.Message);
             }
         */
        }
    }
}
