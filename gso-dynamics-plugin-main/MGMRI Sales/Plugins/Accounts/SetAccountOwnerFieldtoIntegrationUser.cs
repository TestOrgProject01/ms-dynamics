using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Xrm.Sdk;
using Microsoft.Crm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System.IdentityModel.Metadata;
using Helpers;
using System.Runtime.Remoting.Messaging;

namespace MGMRI_Sales.Plugins.Accounts
{
    public class SetAccountOwnerFieldtoIntegrationUser : IPlugin
    {
        public void Execute(IServiceProvider serviceProvider)
        {
            try
            {
                IPluginExecutionContext context = (IPluginExecutionContext)serviceProvider.GetService(typeof(IPluginExecutionContext));
                ITracingService tracingService = (ITracingService)serviceProvider.GetService(typeof(ITracingService));
                IOrganizationServiceFactory serviceFactory = (IOrganizationServiceFactory)serviceProvider.GetService(typeof(IOrganizationServiceFactory));
                IOrganizationService service = (IOrganizationService)serviceFactory.CreateOrganizationService(context.UserId);

                tracingService.LogStart();
                tracingService.LogEntryPoint(context);
                Entity entAcc = null;
                Guid integrationUser = Guid.Empty;

                if (!context.IsValidTargetEntity())
                {
                    tracingService.LogInvalidTarget();
                    tracingService.LogComplete();
                    return;
                }

                tracingService.LogValidTarget();

                // Obtain the target entity from the input parameters.  
                entAcc = (Entity)context.InputParameters["Target"];
                tracingService.Trace("Account Owner " + entAcc.Contains("ownerid"));
                if (!entAcc.Contains("ownerid"))
                {
                    tracingService.Trace("No Owner Id received.");
                    tracingService.LogComplete();
                    return;
                }

                tracingService.Trace("Getting integration user...");
                integrationUser = getIntegrationUser(service);
                tracingService.Trace($"Integration User {integrationUser}");
                if (integrationUser == Guid.Empty || integrationUser == null)
                {
                    tracingService.Trace("Blank Integration User. Nothing else to do...");
                    tracingService.LogComplete();
                    return;
                }

                // This was the original if statement. Seems a bit confusing so rewrote for early exit,
                // if (integrationUser != Guid.Empty || integrationUser != null)
                // {
                tracingService.Trace("Assigning Owner as Integration User");
                EntityReference owner = new EntityReference("systemuser", integrationUser);
                entAcc["ownerid"] = owner;
                // }
                tracingService.LogComplete();
                return;
            }
            catch (InvalidPluginExecutionException e)
            {
                throw new InvalidPluginExecutionException(e.Message);
            }
        }
        public Guid getIntegrationUser(IOrganizationService service)
        {
            //changed the integration user detailed from # crm-dynamic-sp-uw-d to # crminc-ga-sp-dyn-p-2 61st line
            string fetchXml = @"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
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
