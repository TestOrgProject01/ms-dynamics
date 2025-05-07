using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Xrm.Sdk;

namespace MGMRI_Sales.Plugins.Inquiries
{
    public class PreCreateorUpdateInquiry : IPlugin
    {
        public void Execute(IServiceProvider serviceProvider)
        {
            // Obtain the tracing service
            ITracingService tracingService = (ITracingService)
                serviceProvider.GetService(typeof(ITracingService));

            // Obtain the execution context from the service provider.
            IPluginExecutionContext context = (IPluginExecutionContext)
                serviceProvider.GetService(typeof(IPluginExecutionContext));

            // The InputParameters collection contains all the data passed in the message request.
            if (
                context.InputParameters.Contains("Target")
                && context.InputParameters["Target"] is Entity
            )
            {
                // Obtain the target entity from the input parameters.
                Entity entity = (Entity)context.InputParameters["Target"];

                // Obtain the IOrganizationService instance which you will need for
                // web service calls.
                IOrganizationServiceFactory serviceFactory = (IOrganizationServiceFactory)
                    serviceProvider.GetService(typeof(IOrganizationServiceFactory));
                IOrganizationService service = serviceFactory.CreateOrganizationService(
                    context.UserId
                );

                try
                {
                    tracingService.Trace("The plugin depth is:" + context.Depth.ToString());
                    if (entity.Attributes.Contains("gc_inquiryname"))
                    {
                        if (context.Depth == 1)
                        {
                            string inquiryName = (string)entity["gc_inquiryname"];
                            tracingService.Trace("Setting Full Name : " + inquiryName);
                            if (inquiryName.Length <= 80)
                                entity["fullname"] = inquiryName;
                            tracingService.Trace("Set Full Name: " + entity["fullname"]);
                        }
                    }
                }
                catch (FaultException<OrganizationServiceFault> ex)
                {
                    throw new InvalidPluginExecutionException("Error: ", ex);
                }
            }
        }
    }
}
