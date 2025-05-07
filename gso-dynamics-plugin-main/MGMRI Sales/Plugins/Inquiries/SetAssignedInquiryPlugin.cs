using System;
using System.Collections.Generic;
using System.IdentityModel.Metadata;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Helpers;
using Microsoft.Crm.Sdk;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using Newtonsoft.Json;

namespace MGMRI_Sales.Plugins.Inquiries
{
    public class SetAssignedInquiryPlugin : IPlugin
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
                IOrganizationService orgService = (IOrganizationService)
                    serviceFactory.CreateOrganizationService(context.UserId);

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

                tracingService.Trace(context.ParentContext.PrimaryEntityName);

                Entity entInquiry = (Entity)context.InputParameters["Target"];

                switch (context.MessageName.ToUpper())
                {
                    case "CREATE":
                        tracingService.Trace("Message Name " + context.MessageName);
                        ProcessMessage_Create(context, tracingService, orgService, entInquiry);
                        tracingService.LogComplete();
                        return;
                    case "UPDATE":
                        tracingService.Trace("Message Name " + context.MessageName);
                        ProcessMessage_Update(context, tracingService, orgService, entInquiry);
                        tracingService.LogComplete();
                        return;
                }
            }
            catch (InvalidPluginExecutionException ex)
            {
                throw new InvalidPluginExecutionException("Error " + ex.Message);
            }
        }

        private bool ProcessMessage_Update(
            IPluginExecutionContext context,
            ITracingService tracingService,
            IOrganizationService orgService,
            Entity entInquiry
        )
        {
            tracingService.Trace("Message Name is Update ");
            bool flagAssigned = false;
            // Check if this is a cascade delete
            // tracingService.Trace(context.ParentContext.PrimaryEntityName);
            // tracingService.Trace(context.ParentContext.MessageName);
            if (
                context.ParentContext != null
                && context.ParentContext.MessageName == "Delete"
                && context.ParentContext.PrimaryEntityName == "contact"
            )
            {
                // This is a cascade delete from the contact entity
                tracingService.Trace("step1");
                tracingService.Trace(context.ParentContext.PrimaryEntityName);
                tracingService.Trace(context.ParentContext.MessageName);
                // tracingService.Trace("step2");
                try
                {
                    //preimage and post image startas local variables

                    Entity preImageInquiry = null;
                    if (context.PreEntityImages.Contains("PreImageInquiry"))
                    {
                        preImageInquiry = context.PreEntityImages["PreImageInquiry"] as Entity;
                    }
                    else
                    {
                        tracingService.Trace("PreImageInquiry key not found in PreEntityImages.");
                    }

                    Entity postImageInquiry = null;
                    if (context.PostEntityImages.Contains("PostImageInquiry"))
                    {
                        postImageInquiry = context.PostEntityImages["PostImageInquiry"] as Entity;
                    }
                    else
                    {
                        tracingService.Trace("PostImageInquiry key not found in PostEntityImages.");
                    }

                    // Retrieving PreImage and PostImage
                    // Entity preImageInquiry = context.PreEntityImages.Contains("PreImageInquiry") ? context.PreEntityImages["PreImageInquiry"] as Entity : null;
                    // Entity postImageInquiry = context.PostEntityImages.Contains("PostImageInquiry") ? context.PostEntityImages["PostImageInquiry"] as Entity : null;
                    tracingService.Trace("Step1.1");
                    string preImageJson =
                        preImageInquiry != null
                            ? JsonConvert.SerializeObject(preImageInquiry.Attributes)
                            : "";
                    string postImageJson =
                        postImageInquiry != null
                            ? JsonConvert.SerializeObject(postImageInquiry.Attributes)
                            : "";

                    tracingService.Trace(preImageJson);
                    tracingService.Trace(postImageJson);
                    if (preImageInquiry == null || postImageInquiry == null)
                    {
                        tracingService.Trace("Step1.2");
                        tracingService.Trace("PreImage or PostImage is missing, returning...");
                        return false;
                    }
                    else if (
                        preImageInquiry.Contains("gc_contact")
                        && preImageInquiry["gc_contact"] != null
                    )
                    //(postImageInquiry.Contains("gc_contact") && postImageInquiry["gc_contact"] == null)  )

                    {
                        tracingService.Trace("Step1.3");
                        tracingService.Trace("Updating contact ID");
                        Entity InquiryObj = new Entity();
                        InquiryObj.LogicalName = "lead";
                        InquiryObj.Id = entInquiry.Id;
                        InquiryObj.Attributes["gc_contact"] = preImageInquiry["gc_contact"];
                        orgService.Update(InquiryObj);
                    }
                }
                catch (Exception ex)
                {
                    tracingService.Trace(ex.ToString());
                }
                return false; // Exit the plugin to prevent further processing
            }
            tracingService.Trace("step2");

            Entity updateInquiry = orgService.Retrieve(
                "lead",
                entInquiry.Id,
                new ColumnSet("gc_account", "gc_contact", "gc_agent", "gc_agency")
            );
            //Account
            if (updateInquiry.Contains("gc_account") && updateInquiry["gc_account"] != null)
            {
                Guid accountID = ((EntityReference)updateInquiry["gc_account"]).Id;
                tracingService.Trace("Account " + accountID);
                flagAssigned = isGSOAssigned(
                    "account",
                    accountID,
                    "gc_primarygso",
                    "gc_secondarygso",
                    orgService
                );
            }
            //Contact
            tracingService.Trace(context.ParentContext.PrimaryEntityName);
            if (
                updateInquiry.Contains("gc_contact")
                && updateInquiry["gc_contact"] != null
                && !flagAssigned
            )
            {
                //Entity PreImageContact = context.PreEntityImages["PreImage"] as Entity;
                Guid contactID = ((EntityReference)updateInquiry["gc_contact"]).Id;
                tracingService.Trace("Contact " + contactID);
                flagAssigned = isGSOAssigned(
                    "contact",
                    contactID,
                    "gc_gso",
                    "gc_secondarygso",
                    orgService
                );
            }
            //Agency
            if (
                updateInquiry.Contains("gc_agency")
                && updateInquiry["gc_agency"] != null
                && !flagAssigned
            )
            {
                Guid agencyID = ((EntityReference)updateInquiry["gc_agency"]).Id;
                tracingService.Trace("agency " + agencyID);
                flagAssigned = isGSOAssigned(
                    "account",
                    agencyID,
                    "gc_primarygso",
                    "gc_secondarygso",
                    orgService
                );
            }
            //Agent
            if (
                updateInquiry.Contains("gc_agent")
                && updateInquiry["gc_agent"] != null
                && !flagAssigned
            )
            {
                Guid agentID = ((EntityReference)updateInquiry["gc_agent"]).Id;
                tracingService.Trace("Agent " + agentID);
                flagAssigned = isGSOAssigned(
                    "contact",
                    agentID,
                    "gc_gso",
                    "gc_secondarygso",
                    orgService
                );
            }

            tracingService.Trace("Flag Assigned : " + flagAssigned);
            setAssignedInquiryFlag(
                updateInquiry,
                flagAssigned,
                orgService,
                tracingService,
                context.MessageName,
                context.Stage
            );
            return true;
        }

        private void ProcessMessage_Create(
            IPluginExecutionContext context,
            ITracingService tracingService,
            IOrganizationService orgService,
            Entity entInquiry
        )
        {
            tracingService.Trace("Message Name is Create");
            bool flagAssigned = false;
            //Account
            if (entInquiry.Contains("gc_account") && entInquiry["gc_account"] != null)
            {
                Guid accountID = ((EntityReference)entInquiry["gc_account"]).Id;
                flagAssigned = isGSOAssigned(
                    "account",
                    accountID,
                    "gc_primarygso",
                    "gc_secondarygso",
                    orgService
                );
            }
            //Contact
            tracingService.Trace("48");
            if (
                entInquiry.Contains("gc_contact")
                && entInquiry["gc_contact"] != null
                && !flagAssigned
            )
            {
                Guid contactID = ((EntityReference)entInquiry["gc_contact"]).Id;
                flagAssigned = isGSOAssigned(
                    "contact",
                    contactID,
                    "gc_gso",
                    "gc_secondarygso",
                    orgService
                );
            }
            //Agency
            if (
                entInquiry.Contains("gc_agency")
                && entInquiry["gc_agency"] != null
                && !flagAssigned
            )
            {
                Guid agencyID = ((EntityReference)entInquiry["gc_agency"]).Id;
                flagAssigned = isGSOAssigned(
                    "account",
                    agencyID,
                    "gc_primarygso",
                    "gc_secondarygso",
                    orgService
                );
            }
            //Agent
            if (entInquiry.Contains("gc_agent") && entInquiry["gc_agent"] != null && !flagAssigned)
            {
                Guid agentID = ((EntityReference)entInquiry["gc_agent"]).Id;
                flagAssigned = isGSOAssigned(
                    "contact",
                    agentID,
                    "gc_gso",
                    "gc_secondarygso",
                    orgService
                );
            }
            tracingService.Trace("Flag Assigned : " + flagAssigned);
            setAssignedInquiryFlag(
                entInquiry,
                flagAssigned,
                orgService,
                tracingService,
                context.MessageName,
                context.Stage
            );
        }

        public bool isGSOAssigned(
            string entityLogicalName,
            Guid entityId,
            string primaryGSOAttrName,
            string secGSOAttrName,
            IOrganizationService service
        )
        {
            Entity ent = service.Retrieve(
                entityLogicalName,
                entityId,
                new ColumnSet(primaryGSOAttrName, secGSOAttrName)
            );
            if (ent != null)
            {
                if (ent.Contains(primaryGSOAttrName) || ent.Contains(secGSOAttrName))
                {
                    return true;
                }
            }
            return false;
        }

        public void setAssignedInquiryFlag(
            Entity entInquiry,
            bool flagAssigned,
            IOrganizationService service,
            ITracingService tracingService,
            string message,
            int stage
        )
        {
            tracingService.Trace("Inside setAssignedInquiry " + flagAssigned);
            if (entInquiry.Contains("gc_assignedinquiry") && message == "Create" && stage == 10)
            {
                entInquiry["gc_assignedinquiry"] = flagAssigned;
            }
            else
            {
                tracingService.Trace("Inside Update");
                Entity entUpdateInquiry = new Entity("lead", entInquiry.Id);
                entUpdateInquiry["gc_assignedinquiry"] = flagAssigned;
                service.Update(entUpdateInquiry);
            }
        }
    }
}
