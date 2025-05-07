using System;
using System.Collections.Generic;
using System.IdentityModel.Metadata;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using Helpers;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Metadata;
using Microsoft.Xrm.Sdk.Query;

namespace MGMRI_Sales.Plugins.Inquiries
{
    public class CopyLeadSourceIntoLeadSourceUser : IPlugin
    {
        public void Execute(IServiceProvider serviceProvider)
        {
            try
            {
                IPluginExecutionContext context = (IPluginExecutionContext)
                    serviceProvider.GetService(typeof(IPluginExecutionContext));
                IOrganizationServiceFactory serviceFactory = (IOrganizationServiceFactory)
                    serviceProvider.GetService(typeof(IOrganizationServiceFactory));
                IOrganizationService service = serviceFactory.CreateOrganizationService(
                    context.UserId
                );
                ITracingService tracingService = (ITracingService)
                    serviceProvider.GetService(typeof(ITracingService));

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

                Guid inquiryId = Guid.Empty;
                Guid agencyId = Guid.Empty;
                Guid bookingId = Guid.Empty;

                Entity entInquiry = null;
                string leadSource;
                string secondaryLeadSource;
                Entity user;

                EntityCollection ecLeadUser = null;
                EntityCollection ecSecondaryLeadUser = null;
                Guid leadSourceUserId = Guid.Empty;
                Guid secondaryLeadSourceUserId = Guid.Empty;
                Entity updateInquiry = null;
                int leadSourceValue;
                int secondaryLeadSourceValue;

                // Obtain the target entity from the input parameters.
                entInquiry = (Entity)context.InputParameters["Target"];
                if (
                    entInquiry.Contains("gc_leadsource")
                    && ((OptionSetValue)entInquiry["gc_leadsource"]) != null
                )
                {
                    tracingService.Trace("Found Lead Source.");
                    leadSourceValue = ((OptionSetValue)entInquiry["gc_leadsource"]).Value;
                    leadSource = GetOptionSetTextFromValue(
                        "lead",
                        "gc_leadsource",
                        leadSourceValue,
                        service
                    );
                    //leadSource = entInquiry.FormattedValues["gc_leadsource"].ToString().Substring(7);//GSO - Shreya
                    tracingService.Trace("lead Source " + leadSource);
                    if (leadSource.Contains("GSO - "))
                    {
                        tracingService.Trace("Lead Source Contains GSO -");
                        //string Fname = leadSource.Split(' ')[0].ToString();
                        //string Lname = leadSource.Split(' ')[1].ToString();
                        //ecLeadUser = retrieveUser(Fname, Lname, service);//
                        ecLeadUser = retrieveUser(leadSource.ToString().Substring(6), service);
                        tracingService.Trace("Lead Source user " + ecLeadUser);
                        if (ecLeadUser != null && ecLeadUser.Entities.Count > 0)
                        {
                            tracingService.Trace(
                                $"Found Lead Source User Id {ecLeadUser.Entities[0].Id}"
                            );
                            leadSourceUserId = ecLeadUser.Entities[0].Id;
                        }
                    }
                }

                if (
                    entInquiry.Contains("gc_secondaryleadsource")
                    && ((OptionSetValue)entInquiry["gc_secondaryleadsource"]) != null
                )
                {
                    tracingService.Trace("Found Secondary Lead Source.");
                    secondaryLeadSourceValue = (
                        (OptionSetValue)entInquiry["gc_secondaryleadsource"]
                    ).Value;
                    secondaryLeadSource = GetOptionSetTextFromValue(
                        "lead",
                        "gc_secondaryleadsource",
                        secondaryLeadSourceValue,
                        service
                    );

                    tracingService.Trace("lead Source " + secondaryLeadSource);
                    if (secondaryLeadSource.Contains("GSO - "))
                    {
                        tracingService.Trace("Secondary Lead Source Contains GSO -");
                        //string Fname = secondaryLeadSource.Split(' ')[0].ToString();
                        //string Lname = secondaryLeadSource.Split(' ')[1].ToString();
                        //ecSecondaryLeadUser = retrieveUser(Fname, Lname, service);
                        ecSecondaryLeadUser = retrieveUser(
                            secondaryLeadSource.ToString().Substring(6),
                            service
                        );
                        tracingService.Trace("Sec lead Source user " + ecSecondaryLeadUser);
                        if (ecSecondaryLeadUser != null && ecSecondaryLeadUser.Entities.Count > 0)
                        {
                            tracingService.Trace(
                                $"Found Secondary Lead Source User Id {ecSecondaryLeadUser.Entities[0].Id}"
                            );
                            secondaryLeadSourceUserId = ecSecondaryLeadUser.Entities[0].Id;
                        }
                    }
                }

                switch (context.MessageName.ToUpper())
                {
                    case "CREATE":
                        tracingService.Trace("Message Name " + context.MessageName);
                        if (context.Stage.Equals(20))
                        {
                            tracingService.Trace("Stage 20 ");
                            if (entInquiry.Attributes.Contains("gc_leadsourceusers"))
                            {
                                tracingService.Trace("Lead Source users found...");
                                if (!(leadSourceUserId == Guid.Empty) && leadSourceUserId != null)
                                {
                                    tracingService.Trace("Setting gc_leadsourceusers...");
                                    entInquiry["gc_leadsourceusers"] = new EntityReference(
                                        "systemuser",
                                        leadSourceUserId
                                    );
                                }
                            }
                            if (entInquiry.Attributes.Contains("gc_secondaryleadsourceusers"))
                            {
                                if (
                                    !(secondaryLeadSourceUserId == Guid.Empty)
                                    && secondaryLeadSourceUserId != null
                                )
                                    tracingService.Trace("Secondary Lead Source users found...");
                                tracingService.Trace("Setting gc_secondaryleadsourceusers...");
                                entInquiry["gc_secondaryleadsourceusers"] = new EntityReference(
                                    "systemuser",
                                    secondaryLeadSourceUserId
                                );
                            }
                            tracingService.LogComplete();
                            return;
                        }
                        break;
                    default:
                        tracingService.Trace("Message Name " + context.MessageName);
                        break;
                }

                if (context.Depth != 1)
                {
                    tracingService.Trace("Context depth != 1. Exiting...");
                    tracingService.LogComplete();
                    return;
                }

                if (
                    (context.MessageName.Equals("Update") || context.MessageName.Equals("Create"))
                    && context.Stage.Equals(40)
                )
                {
                    tracingService.Trace("Stage 40 ");
                    updateInquiry = new Entity("lead", entInquiry.Id);
                    if (!(leadSourceUserId == Guid.Empty) && leadSourceUserId != null)
                    {
                        tracingService.Trace("Lead Source users found...");
                        updateInquiry["gc_leadsourceusers"] = new EntityReference(
                            "systemuser",
                            leadSourceUserId
                        );
                    }
                    if (
                        !(secondaryLeadSourceUserId == Guid.Empty)
                        && secondaryLeadSourceUserId != null
                    )
                    {
                        tracingService.Trace("Secondary Lead Source users found...");
                        updateInquiry["gc_secondaryleadsourceusers"] = new EntityReference(
                            "systemuser",
                            secondaryLeadSourceUserId
                        );
                    }
                    service.Update(updateInquiry);
                }

                tracingService.LogComplete();
            }
            catch (InvalidPluginExecutionException ex)
            {
                throw new InvalidPluginExecutionException("Error " + ex.Message);
            }
        }

        public EntityCollection retrieveUser(string fullname, IOrganizationService service)
        {
            string fetchXML =
                @"<fetch version='1.0' mapping='logical' distinct='false'>
                                  <entity name='systemuser'>
                                    <attribute name='fullname' />
                                    <attribute name='systemuserid' />
                                    <attribute name='lastname' />
                                    <attribute name='firstname' />
                                    <filter type='and'>
                                      <condition attribute='fullname' operator='eq' value='"
                + fullname
                + @"' />
                                      <condition attribute='isdisabled' operator='eq' value='0' />                                      
                                    </filter>
                                  </entity>
                                </fetch>";

            EntityCollection ecUsers = service.RetrieveMultiple(new FetchExpression(fetchXML));
            return ecUsers;
        }

        public static string GetOptionSetTextFromValue(
            string entityName,
            string attributeName,
            int value,
            IOrganizationService service
        )
        {
            string optionsetText = string.Empty;
            var attributeRequest = new RetrieveAttributeRequest
            {
                EntityLogicalName = entityName,
                LogicalName = attributeName,
                RetrieveAsIfPublished = true,
            };
            // Execute the request.
            var attributeResponse = (RetrieveAttributeResponse)service.Execute(attributeRequest);
            // Access the retrieved attribute.
            var attributeMetadata = (EnumAttributeMetadata)attributeResponse.AttributeMetadata;
            // Get the current options list for the retrieved attribute.

            var optionList = (
                from o in attributeMetadata.OptionSet.Options
                select new { Value = o.Value, Text = o.Label.UserLocalizedLabel.Label }
            ).ToList();

            optionsetText = optionList
                .Where(o => o.Value == value)
                .Select(o => o.Text)
                .FirstOrDefault();
            return optionsetText;
        }
    }
}
