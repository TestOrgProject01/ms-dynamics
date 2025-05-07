using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Helpers;
using Microsoft.Crm.Sdk;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;

namespace MGMRI_Sales.Plugins.Inquiries
{
    public class SetOwnerFieldtoIntegrationUser : IPlugin
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

                Entity entInquiry = null;
                Guid integrationUser = Guid.Empty;

                // Log start
                tracingService.LogStart();
                tracingService.LogEntryPoint(context);

                // PreChecks
                if (!context.IsValidTargetEntity())
                {
                    tracingService.LogInvalidTarget();
                    tracingService.LogComplete();
                    return;
                }

                // Obtain the target entity from the input parameters.
                entInquiry = (Entity)context.InputParameters["Target"];
                tracingService.Trace("Inquiry Owner " + entInquiry.Contains("ownerid"));
                if (!entInquiry.Contains("ownerid"))
                {
                    tracingService.Trace("No Owner Id. No Processing Needed...");
                    tracingService.LogComplete();
                    return;
                }
                integrationUser = getIntegrationUser(service);

                if (integrationUser == Guid.Empty && integrationUser == null)
                {
                    tracingService.Trace("Integration User was null. No processing needed.");
                    tracingService.LogComplete();
                    return;
                }

                tracingService.Trace("Integration User " + integrationUser);
                EntityReference owner = new EntityReference("systemuser", integrationUser);
                entInquiry["ownerid"] = owner;
                tracingService.LogComplete();
            }
            catch (InvalidPluginExecutionException e)
            {
                throw new InvalidPluginExecutionException(e.Message);
            }
        }

        public Guid getIntegrationUser(IOrganizationService service)
        {
            //changed the integration user detailed from # crm-dynamic-sp-uw-d to # crminc-ga-sp-dyn-p-2 61st line

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
