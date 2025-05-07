using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Helpers;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;

namespace MGMRI_Sales.Plugins.Activities
{
    public class SetActivityFields : IPlugin
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

                tracingService.LogStart();
                tracingService.LogEntryPoint(context);

                //Entity entPostImageAccount = (Entity)context.PostEntityImages["PostImage_Account"];
                Guid regardingObjectId = Guid.Empty;

                Entity entActivity = null;
                Guid activityId = Guid.Empty;
                EntityReference regardingObjectReference = null;
                string cidlOriginAct = string.Empty;
                EntityCollection ecContacts = null;
                string phoneNumber = string.Empty;
                string email = string.Empty;
                Entity relatedTo = null;
                string targetEntityLogicalName = string.Empty;
                string activityPhoneField = string.Empty;

                if (!context.IsValidTargetEntity())
                {
                    tracingService.LogInvalidTarget();
                }
                else
                {
                    tracingService.LogValidTarget();

                    // Obtain the target entity from the input parameters.
                    entActivity = (Entity)context.InputParameters["Target"];
                    activityId = entActivity.Id;
                    tracingService.Trace($"Activity Id: {activityId} ");
                    targetEntityLogicalName = entActivity.LogicalName;
                    tracingService.Trace($"Entity Logical Name: {targetEntityLogicalName} ");

                    // Setting phone field
                    if (targetEntityLogicalName == "phonecall")
                    {
                        activityPhoneField = "phonenumber";
                    }
                    else
                    {
                        activityPhoneField = "gc_phone";
                    }
                    tracingService.Trace($"Activity Phone Field: {activityPhoneField} ");
                }

                switch (context.MessageName.ToUpper())
                {
                    case "CREATE":
                        tracingService.Trace("Message Name " + context.MessageName);
                        ProcessMessage_Create(
                            context,
                            tracingService,
                            orgService,
                            ref regardingObjectId,
                            activityId,
                            ref regardingObjectReference,
                            ref cidlOriginAct,
                            ref ecContacts,
                            ref phoneNumber,
                            ref email,
                            ref relatedTo,
                            targetEntityLogicalName,
                            activityPhoneField
                        );
                        tracingService.LogComplete();
                        return;
                    case "UPDATE":
                        tracingService.Trace("Message Name " + context.MessageName);
                        ProcessMessage_Update(
                            context,
                            tracingService,
                            orgService,
                            ref regardingObjectId,
                            entActivity,
                            ref regardingObjectReference,
                            ref cidlOriginAct,
                            ref ecContacts,
                            ref phoneNumber,
                            ref email,
                            ref relatedTo,
                            activityPhoneField
                        );
                        tracingService.LogComplete();
                        return;
                    default:
                        tracingService.LogUnsupportedMessageType(context.MessageName);
                        tracingService.LogComplete();
                        return;
                }
            }
            catch (InvalidPluginExecutionException e)
            {
                throw new InvalidPluginExecutionException(e.Message);
            }
        }

        private bool ProcessMessage_Update(
            IPluginExecutionContext context,
            ITracingService tracingService,
            IOrganizationService orgService,
            ref Guid regardingObjectId,
            Entity entActivity,
            ref EntityReference regardingObjectReference,
            ref string cidlOriginAct,
            ref EntityCollection ecContacts,
            ref string phoneNumber,
            ref string email,
            ref Entity relatedTo,
            string activityPhoneField
        )
        {
            if (!context.Stage.Equals(20))
            {
                tracingService.Trace(
                    $"Stage: {context.Stage}. Expected 20. No further processing needed"
                );
                return false;
            }
            if (activityPhoneField == string.Empty)
            {
                tracingService.Trace($"Activity Phone Field Not set. No further processing needed");
                return false;
            }

            tracingService.Trace("Stage: " + context.Stage);
            tracingService.Trace("Message Name" + context.MessageName);
            Entity entPreImageAccount = (Entity)context.PreEntityImages["PreImage_Account"];

            if (
                entPreImageAccount.Contains("mgm_origin_txt")
                && entPreImageAccount["mgm_origin_txt"] != null
            )
            {
                cidlOriginAct = entPreImageAccount["mgm_origin_txt"].ToString();
                tracingService.Trace("Cidl Origin: " + cidlOriginAct);
            }

            if (
                entActivity.Contains("regardingobjectid")
                && entActivity["regardingobjectid"] != null
                && cidlOriginAct == "GSO"
            )
            {
                // Get the regarding
                regardingObjectReference = (EntityReference)entActivity["regardingobjectid"];
                regardingObjectId = regardingObjectReference.Id;
                tracingService.Trace("Regarding Object Id: " + regardingObjectId);

                if (regardingObjectReference.LogicalName != "contact")
                {
                    tracingService.Trace("Regarding was not a contact. No processing needed. ");
                    return false;
                }

                if (activityPhoneField == string.Empty)
                {
                    tracingService.Trace("Activity Phone Field was empty. No processing needed. ");
                    return false;
                }

                // Getting contct details.
                tracingService.Trace("Getting contact details. ");
                ecContacts = getContactDetails(regardingObjectId, orgService, tracingService);
                tracingService.Trace("Count : " + ecContacts.Entities.Count);

                if (ecContacts.Entities.Count == 0)
                {
                    tracingService.Trace("No contacts found. No processing needed. ");
                    return false;
                }

                relatedTo = ecContacts.Entities[0];

                if (relatedTo.Contains("company") && relatedTo["company"] != null)
                {
                    phoneNumber = relatedTo["company"].ToString();
                    tracingService.Trace("Phone : " + phoneNumber);
                    entActivity[activityPhoneField] = phoneNumber;
                }
                else
                {
                    tracingService.Trace("Phone : " + phoneNumber);
                    entActivity[activityPhoneField] = phoneNumber;
                }

                if (relatedTo.Contains("emailaddress1") && relatedTo["emailaddress1"] != null)
                {
                    email = relatedTo["emailaddress1"].ToString();
                    tracingService.Trace("Email : " + email);
                    entActivity["gc_email"] = email;
                }
                else
                {
                    tracingService.Trace("Email : " + email);
                    entActivity["gc_email"] = email;
                }

                entActivity["modifiedby"] = new EntityReference("systemuser", context.UserId);

                return true;
            }

            // Why is this here??
            tracingService.Trace("Inside Else Message Name" + context.MessageName);
            entActivity[activityPhoneField] = phoneNumber;
            entActivity["gc_email"] = email;
            entActivity["modifiedby"] = new EntityReference("systemuser", context.UserId);
            return true;
        }

        private bool ProcessMessage_Create(
            IPluginExecutionContext context,
            ITracingService tracingService,
            IOrganizationService orgService,
            ref Guid regardingObjectId,
            Guid activityId,
            ref EntityReference regardingObjectReference,
            ref string cidlOriginAct,
            ref EntityCollection ecContacts,
            ref string phoneNumber,
            ref string email,
            ref Entity relatedTo,
            string targetEntityLogicalName,
            string activityPhoneField
        )
        {
            if (activityPhoneField == string.Empty)
            {
                tracingService.Trace("Phone Field was empty. Exiting...");
                return false;
            }

            Entity entPostImageAccount = (Entity)
                context.GetPostImage("PostImage_Account", tracingService);
            Entity entUpdateActivity = null;
            if (entPostImageAccount != null)
            {
                if (
                    entPostImageAccount.Contains("regardingobjectid")
                    && entPostImageAccount["regardingobjectid"] != null
                )
                {
                    tracingService.Trace("Found Non-Null regarding Object on PostImage");
                    regardingObjectReference = (EntityReference)
                        entPostImageAccount["regardingobjectid"];
                    regardingObjectId = regardingObjectReference.Id;
                    tracingService.Trace("Regarding Object Id: " + regardingObjectId);
                }

                if (
                    entPostImageAccount.Contains("mgm_origin_txt")
                    && entPostImageAccount["mgm_origin_txt"] != null
                )
                {
                    cidlOriginAct = entPostImageAccount["mgm_origin_txt"].ToString();
                    tracingService.Trace("Cidl Origin: " + cidlOriginAct);
                }
            }

            if (activityId != Guid.Empty && targetEntityLogicalName != string.Empty)
            {
                tracingService.Trace("Creating Entity Wrapper for Update...");
                entUpdateActivity = new Entity(targetEntityLogicalName, activityId);
            }

            if (cidlOriginAct == "GSO" && regardingObjectId != Guid.Empty)
            {
                if (regardingObjectReference.LogicalName == "contact")
                {
                    ecContacts = getContactDetails(regardingObjectId, orgService, tracingService);
                    tracingService.Trace("Count : " + ecContacts.Entities.Count);
                    if (ecContacts.Entities.Count > 0)
                    {
                        relatedTo = ecContacts.Entities[0];

                        if (relatedTo.Contains("company") && relatedTo["company"] != null)
                        {
                            phoneNumber = relatedTo["company"].ToString();
                            tracingService.Trace("Phone : " + phoneNumber);
                            entUpdateActivity[activityPhoneField] = phoneNumber;
                        }
                        else
                        {
                            tracingService.Trace("Phone : " + phoneNumber);
                            entUpdateActivity[activityPhoneField] = phoneNumber;
                        }

                        if (
                            relatedTo.Contains("emailaddress1")
                            && relatedTo["emailaddress1"] != null
                        )
                        {
                            email = relatedTo["emailaddress1"].ToString();
                            tracingService.Trace("Email : " + email);
                            entUpdateActivity["gc_email"] = relatedTo["emailaddress1"].ToString();
                        }
                        else
                        {
                            tracingService.Trace("Email : " + email);
                            entUpdateActivity["gc_email"] = email;
                        }
                        tracingService.Trace("Update Task : ");
                        orgService.Update(entUpdateActivity);
                        return false;
                    }
                }

                tracingService.Trace(
                    "Found Regarding object was not a contact. No further processing needed"
                );
                return false;
            }

            // What is the point of this??
            tracingService.Trace("Message Name" + context.MessageName);
            tracingService.Trace("Inside Else" + context.MessageName);
            entUpdateActivity[activityPhoneField] = phoneNumber;
            entUpdateActivity["gc_email"] = email;
            return true;
        }

        public EntityCollection getContactDetails(
            Guid contactId,
            IOrganizationService service,
            ITracingService trace
        )
        {
            EntityCollection ecContacts = null;
            string fetchXML =
                @"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                          <entity name='contact'>
                            <attribute name='fullname' />
                            <attribute name='contactid' />
                            <attribute name='company' />
                            <attribute name='emailaddress1' />
                            <order attribute='fullname' descending='false' />
                            <filter type='and'>
                              <condition attribute='contactid' operator='eq' uiname='Aaron Beck' uitype='contact' value='"
                + contactId
                + @"' />
                            </filter>
                          </entity>
                        </fetch>";
            ecContacts = service.RetrieveMultiple(new FetchExpression(fetchXML));
            return ecContacts;
        }
    }
}
