using System;
using Helpers;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;

namespace MGMRI_Sales.Plugins.Accounts
{
    // Deprecated. This logic is not handled by the GSO-Pugins.dll
    public class SetRelatedAccountOnContact : IPlugin
    {
        public void Execute(IServiceProvider serviceProvider)
        {
            try
            {

                // Get services
                IPluginExecutionContext context = (IPluginExecutionContext)
                    serviceProvider.GetService(typeof(IPluginExecutionContext));
                ITracingService tracingService = (ITracingService)
                    serviceProvider.GetService(typeof(ITracingService));
                IOrganizationServiceFactory serviceFactory = (IOrganizationServiceFactory)
                    serviceProvider.GetService(typeof(IOrganizationServiceFactory));
                IOrganizationService service = (IOrganizationService)
                    serviceFactory.CreateOrganizationService(context.UserId);

                // Log start
                tracingService.LogStart();
                tracingService.LogEntryPoint(context);

                // All relevant data
                Entity entTarget = null;
                Guid accountId = Guid.Empty;
                EntityReference entContactRef = null;
                Guid contactID = Guid.Empty;
                Entity entContact = null;

                tracingService.Trace("Acquiring Post Image...");
                Entity entPostImageAccount = (Entity)context.GetPostImage("PostImage_Account", tracingService);

                string cidlOriginAcc = string.Empty;
                // Get cidl origin
                if (
                    entPostImageAccount.Contains("mgm_origin_txt")
                    && entPostImageAccount["mgm_origin_txt"] != null
                )
                {
                    tracingService.Trace("Acquiring Cidl Origin from Post Image...");
                    cidlOriginAcc = entPostImageAccount["mgm_origin_txt"].ToString();
                    tracingService.Trace("Cidl Origin: " + cidlOriginAcc);
                }
                else
                {
                    tracingService.Trace("PostImage does not contain Cidl Origin...");
                }

                // Get Prim Contact
                if (
                    entPostImageAccount.Contains("primarycontactid")
                    && entPostImageAccount["primarycontactid"] != null
                )
                {
                    tracingService.Trace("Acquiring Primary Contact Id from Post Image...");
                    entContactRef = (EntityReference)entPostImageAccount["primarycontactid"];
                    tracingService.Trace("Contact iD: " + entContactRef.Id);
                }
                else
                {
                    tracingService.Trace("No Primary Contact on Post Image...");
                }

                if (
                    context.InputParameters.Contains("Target")
                    && context.InputParameters["Target"] is Entity
                )
                {
                    tracingService.Trace("Acquiring Target...");
                    // Obtain the target entity from the input parameters.
                    entTarget = (Entity)context.InputParameters["Target"];
                    tracingService.Trace("Acquiring Target Id...");
                    accountId = entTarget.Id;
                    tracingService.Trace("Account Id " + accountId);
                    if (cidlOriginAcc != string.Empty && cidlOriginAcc == "GSO")
                    {
                        tracingService.Trace("Cidl Origin was not null...");
                        if (entContactRef != null)
                        {
                            tracingService.Trace("Contact Ref was not null");
                            string callingUsername = service
                                .Retrieve("systemuser", context.UserId, new ColumnSet("fullname"))
                                .GetAttributeValue<string>("fullname");
                            tracingService.Trace("Calling User : " + callingUsername);
                            entContact = service.Retrieve(
                                "contact",
                                entContactRef.Id,
                                new ColumnSet("contactid", "hsl_cidlorigin_txt", "modifiedby")
                            );
                            EntityReference modByUser = (EntityReference)entContact["modifiedby"];
                            string contacModifiedByUser = modByUser.Name;
                            tracingService.Trace(
                                "Contact Modified By User : " + contacModifiedByUser
                            );
                            //changed the integration user detailed from # crm-dynamic-sp-uw-d to # crminc-ga-sp-dyn-p-2 59th and 70th line

                            if (
                                entContact.Contains("hsl_cidlorigin_txt")
                                && (string)entContact["hsl_cidlorigin_txt"] == "GSO"
                                && callingUsername != "# crminc-ga-sp-dyn-p-2"
                            )
                            {
                                entContact["gc_accountname"] = new EntityReference(
                                    "account",
                                    accountId
                                );
                                service.Update(entContact);
                            }
                            //else if (entContact.Contains("hsl_cidlorigin_txt") && (string)entContact["hsl_cidlorigin_txt"] == "GSO" && callingUsername == "# crm-dynamic-sp-uw-d")
                            //{
                            //    entContact["modifiedby"] = new EntityReference("systemuser", modByUser.Id);
                            //    entContact["gc_accountname"] = new EntityReference("account", accountId);
                            //    service.Update(entContact);
                            //}
                            else if (
                                entContact.Contains("hsl_cidlorigin_txt")
                                && (string)entContact["hsl_cidlorigin_txt"] == "GSO"
                                && callingUsername == "# crminc-ga-sp-dyn-p-2"
                            )
                            {
                                return;
                            }
                        }
                    }
                }
            }
            catch (InvalidPluginExecutionException e)
            {
                throw new InvalidPluginExecutionException(e.Message);
            }
        }
    }
}
