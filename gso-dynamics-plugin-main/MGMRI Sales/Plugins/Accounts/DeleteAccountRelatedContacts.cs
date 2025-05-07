using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Helpers;
using Microsoft.Crm.Sdk;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;

namespace MGMRI_Sales.Plugins.Accounts
{
    public class DeleteAccountRelatedContacts : IPlugin
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

                tracingService.LogStart();
                tracingService.LogEntryPoint(context);
                Guid accountID = Guid.Empty;
                Entity preImageAccount = (Entity)context.GetPreImage("PreImage_AccountId", tracingService);

                if (preImageAccount.Contains("accountid"))
                {
                    // Obtain the target entity from the input parameters.
                    //entAccount = (Entity)context.InputParameters["Target"];
                    accountID = (Guid)preImageAccount["accountid"];
                    retrieveAndDeleteRelatedContacts(service, accountID, tracingService);
                    //ecContacts = service.RetrieveMultiple()
                }
                tracingService.LogComplete();
            }
            catch (InvalidPluginExecutionException ex)
            {
                throw new InvalidPluginExecutionException("Error " + ex.Message);
            }
        }

        public void retrieveAndDeleteRelatedContacts(
            IOrganizationService service,
            Guid accId,
            ITracingService trace
        )
        {
            trace.Trace("Inside retrieve : ");
            EntityCollection ecContacts = null;
            string fetchXml =
                @"<fetch version='1.0' mapping='logical' distinct='false'>
                          <entity name='contact'>
                            <attribute name='fullname' />
                            <attribute name='contactid' />
                            <attribute name='hsl_cidlorigin_txt' />
                            <attribute name='gc_accountname' />
                            <order attribute='fullname' descending='false' />
                            <filter type='and'>
                              <condition attribute='gc_accountname' operator='eq' uitype='account' value='{"
                + accId
                + @"}' />
                              <condition attribute='statecode' operator='eq' value='0' />
                              <filter type='and'>
                                <condition attribute='hsl_cidlorigin_txt' operator='not-null' />
                                <condition attribute='hsl_cidlorigin_txt' operator='eq' value='GSO' />
                              </filter>
                            </filter>
                            <link-entity name='account' from='accountid' to='gc_accountname' visible='false' link-type='outer' alias='ac'>
                              <attribute name='name' />
                            </link-entity>
                          </entity>
                        </fetch>";

            ecContacts = service.RetrieveMultiple(new FetchExpression(fetchXml));
            trace.Trace(" Count : " + ecContacts.Entities.Count);
            if (ecContacts.Entities.Count > 0)
            {
                foreach (Entity contact in ecContacts.Entities)
                {
                    Guid contactId = contact.Id;
                    //entity entContact = new Entity("contact",contactId);
                    service.Delete("contact", contactId);
                    trace.Trace("Record deleted : " + contactId);
                }
            }
        }
    }
}
