using System;
using System.Security.Principal;
using Helpers;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;

namespace MGMRI_Sales.Plugins.SalesInitiativeaccountlookup
{
    public class SalesInitiativeAccountLookup : IPlugin
    {
        IPluginExecutionContext context = null;

        public void Execute(IServiceProvider serviceProvider)
        {
            try
            {
                context = (IPluginExecutionContext)
                    serviceProvider.GetService(typeof(IPluginExecutionContext));
                ITracingService tracingService = (ITracingService)
                    serviceProvider.GetService(typeof(ITracingService));
                IOrganizationServiceFactory serviceFactory = (IOrganizationServiceFactory)
                    serviceProvider.GetService(typeof(IOrganizationServiceFactory));
                IOrganizationService service = serviceFactory.CreateOrganizationService(
                    context.UserId
                );

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

                // Check the message and entity type
                tracingService.Trace("SalesInitiativeAccountLookup:- Start");
                tracingService.Trace("Message Name " + context.MessageName);

                switch (context.MessageName.ToUpper())
                {
                    case "ASSOCIATE":
                        tracingService.Trace("Message Name " + context.MessageName);
                        ProcessMessage_Associate(tracingService, service);
                        tracingService.LogComplete();
                        return;
                    case "DISASSOCIATE":
                        tracingService.Trace("Message Name " + context.MessageName);
                        ProcessMessage_Disassociate(tracingService, service);
                        tracingService.LogComplete();
                        break;
                    default:
                        tracingService.LogUnsupportedMessageType(context.MessageName);
                        tracingService.LogComplete();
                        return;
                }
            }
            catch (Exception ex)
            {
                // Log the exception
                throw new InvalidPluginExecutionException(
                    "An error occurred in the SalesInitiativeAccountLookup plugin.",
                    ex
                );
            }
        }

        private void ProcessMessage_Disassociate(
            ITracingService tracingService,
            IOrganizationService service
        )
        {
            // Get the target entity reference
            EntityReference targetEntity = (EntityReference)context.InputParameters["Target"];
            EntityReferenceCollection relatedEntities = (EntityReferenceCollection)
                context.InputParameters["RelatedEntities"];

            tracingService.Trace("SalesInitiativeAccountLookup:- Disassociate");
            foreach (var relatedEntity in relatedEntities)
            {
                // Handle disassociation
                SyncCustomTable(service, targetEntity, relatedEntity, false, tracingService);
            }
        }

        private void ProcessMessage_Associate(
            ITracingService tracingService,
            IOrganizationService service
        )
        {
            // Get the target entity reference
            EntityReference targetEntity = (EntityReference)context.InputParameters["Target"];
            EntityReferenceCollection relatedEntities = (EntityReferenceCollection)
                context.InputParameters["RelatedEntities"];

            // Process the association or disassociation
            if (context.MessageName == "Associate")
            {
                tracingService.Trace("SalesInitiativeAccountLookup:- Associate");
                foreach (var relatedEntity in relatedEntities)
                {
                    // Handle association
                    // tracingService.Trace(targetEntity.Name);
                    // tracingService.Trace(relatedEntity.Name);
                    // tracingService.Trace("a", relatedEntity.Id);
                    SyncCustomTable(service, targetEntity, relatedEntity, true, tracingService);
                }
            }
        }

        public void SyncCustomTable(
            IOrganizationService service,
            EntityReference accountid,
            EntityReference gc_salesinitiativesid,
            bool isAssociating,
            ITracingService tracingService
        )
        {
            // Define the custom lookup table entity name
            const string CustomLookupTableName = "gc_salesinitiativesaccountlookup"; // Replace with your custom entity name

            tracingService.Trace("isassociation started");
            if (isAssociating)
            {
                // Create a new record in the custom lookup table
                Entity salesinitiativeaccountlookup = new Entity(CustomLookupTableName);

                // Trace the values for debugging
                tracingService.Trace(
                    "Creating record in custom lookup table for Account: {0}, Sales Initiative: {1}",
                    accountid.Id,
                    gc_salesinitiativesid.Id
                );

                ColumnSet accountcolumnSet = new ColumnSet("gc_amadeusintegrationreferenceid");
                Entity accountInfo = service.Retrieve("account", accountid.Id, accountcolumnSet);

                ColumnSet salescolumnSet = new ColumnSet("gc_amadeusintegrationreferenceid");
                Entity salesInfo = service.Retrieve(
                    "gc_salesinitiatives",
                    gc_salesinitiativesid.Id,
                    salescolumnSet
                );

                // Set the values for the custom lookup table fields
                //  salesinitiativeaccountlookup["gc_accountdynguid"] = accountid; // Use EntityReference gc_accountdynguid to need to try old value "gc_accountamadeusid"
                //  salesinitiativeaccountlookup["gc_salesinitiativeguid"] = gc_salesinitiativesid; // Use EntityReference gc_salesinitiativeguid to need to try old value "gc_salesinitiativeamadeusid"

                EntityReference accountRef = new EntityReference("account", accountid.Id);
                EntityReference salesInitiativeRef = new EntityReference(
                    "gc_salesinitiatives",
                    gc_salesinitiativesid.Id
                );
                salesinitiativeaccountlookup["gc_accountdynguid"] = accountRef; // Use EntityReference gc_accountdynguid to need to try old value "gc_accountamadeusid"
                salesinitiativeaccountlookup["gc_salesinitiativeguid"] = salesInitiativeRef; // Use EntityReference gc_salesinitiativeguid to need to try old value "gc_salesinitiativeamadeusid"

                salesinitiativeaccountlookup["gc_accountamadeusid"] = accountInfo[
                    "gc_amadeusintegrationreferenceid"
                ]; // Use EntityReference gc_accountdynguid to need to try old value "gc_accountamadeusid"

                salesinitiativeaccountlookup["gc_salesinitiativeamadeusid"] = salesInfo[
                    "gc_amadeusintegrationreferenceid"
                ];

                /*  Entity accountentityID = service.Retrieve("account", accid, new ColumnSet("gc_amadeusintegrationreferenceid"));

                  //  Entity accountentityID = service.Retrieve("account", accountid.Id, new ColumnSet("gc_amadeusintegrationreferenceid"));
                  tracingService.Trace("acc",accountentityID);
                  if (accountentityID.Contains("gc_amadeusintegrationreferenceid"))
                  {
                      string accountName = accountentityID["gc_amadeusintegrationreferenceid"].ToString();
                      tracingService.Trace("accounentityid name", accountName);
                  }
                  var accountentityIDIntegrationReferenceId = accountentityID.GetAttributeValue<Guid?>("gc_amadeusintegrationreferenceid");
                       tracingService.Trace("acc int id",accountentityIDIntegrationReferenceId);
                      /* var salesInitiativeentityID = service.Retrieve(gc_salesinitiativesid.LogicalName, gc_salesinitiativesid.Id, new ColumnSet("gc_amadeusintegrationreferenceid"));
                       tracingService.Trace("sales ", salesInitiativeentityID);
                       var salesInitiativeentityIDIntegrationReferenceId = salesInitiativeentityID.GetAttributeValue<Guid?>("gc_amadeusintegrationreferenceid");
                       tracingService.Trace("sales int id", salesInitiativeentityIDIntegrationReferenceId);
                */

                try
                {
                    // Create the record in the custom lookup table
                    service.Create(salesinitiativeaccountlookup);
                    tracingService.Trace("Record created successfully.");
                }
                catch (Exception ex)
                {
                    tracingService.Trace("Error while creating record: {0}", ex.Message);
                    // Retry logic (optional, if necessary)
                    System.Threading.Thread.Sleep(2000); // Sleep for 2 seconds (this is just an example)
                    try
                    {
                        service.Create(salesinitiativeaccountlookup);
                        tracingService.Trace("Record created successfully after retry.");
                    }
                    catch (Exception retryEx)
                    {
                        tracingService.Trace("Error after retry: {0}", retryEx.Message);
                    }
                }
            }
            else
            {
                // Disassociation- Remove the record from the custom lookup table gc_statussial
                QueryExpression qe = new QueryExpression(CustomLookupTableName);
                ColumnSet columnSet = new ColumnSet("gc_statussial");

                Entity salesinitiativeaccountlookup = new Entity(
                    CustomLookupTableName,
                    accountid.Id
                );

                // Trace the values for debugging
                tracingService.Trace(
                    "Creating record in custom lookup table for Account: {0}, Sales Initiative: {1}",
                    accountid.Id,
                    gc_salesinitiativesid.Id
                );

                ColumnSet accountcolumnSet = new ColumnSet("gc_amadeusintegrationreferenceid");
                Entity accountInfo = service.Retrieve("account", accountid.Id, accountcolumnSet);

                ColumnSet salescolumnSet = new ColumnSet("gc_amadeusintegrationreferenceid");
                Entity salesInfo = service.Retrieve(
                    "gc_salesinitiatives",
                    gc_salesinitiativesid.Id,
                    salescolumnSet
                );

                EntityReference accountRef = new EntityReference("account", accountid.Id);
                EntityReference salesInitiativeRef = new EntityReference(
                    "gc_salesinitiatives",
                    gc_salesinitiativesid.Id
                );
                salesinitiativeaccountlookup["gc_accountdynguid"] = accountRef; // Use EntityReference gc_accountdynguid to need to try old value "gc_accountamadeusid"
                salesinitiativeaccountlookup["gc_salesinitiativeguid"] = salesInitiativeRef; // Use EntityReference gc_salesinitiativeguid to need to try old value "gc_salesinitiativeamadeusid"

                QueryExpression query = new QueryExpression(CustomLookupTableName)
                {
                    ColumnSet = new ColumnSet("gc_statussial", "gc_amadeusintegrationreferenceid"), // Specify the columns you want to retrieve
                    Criteria = new FilterExpression()
                    {
                        Conditions =
                        {
                            new ConditionExpression(
                                "gc_accountdynguid",
                                ConditionOperator.Equal,
                                accountRef
                            ),
                            new ConditionExpression(
                                "gc_salesinitiativeguid",
                                ConditionOperator.Equal,
                                salesInitiativeRef
                            ),
                        },
                    },
                };
                EntityCollection Result = service.RetrieveMultiple(query);
                OptionSetValue inActive = new OptionSetValue(1); // For example, 2 represents a specific option
                foreach (Entity row in Result.Entities)
                {
                    tracingService.Trace("InActive is Set onside for loop");
                    tracingService.Trace(row["gc_amadeusintegrationreferenceid"].ToString());
                    // row["gc_statussial"] = inActive;
                    // service.Update(row);
                    tracingService.Trace("Row Updated");
                }
                salesinitiativeaccountlookup.Attributes.Add("gc_statussial", inActive);
                tracingService.Trace("Status is set to InActive");
                service.Update(salesinitiativeaccountlookup);
                tracingService.Trace("InActive is Set");
            }
        }
    }
}


/*
private void SyncCustomTable(IOrganizationService service, EntityReference accountid, EntityReference gc_salesinitiativesid, bool isAssociating, ITracingService tracingService)
{
    const string CustomLookupTableName = "gc_salesinitiativesaccountlookup"; // Replace with your custom entity name

    if (isAssociating)
    {
        // Step 1: Retrieve gc_amadeusintegrationreferenceid for accountid
        var account = service.Retrieve(accountid.LogicalName, accountid.Id, new ColumnSet("gc_amadeusintegrationreferenceid"));
        var accountIntegrationReferenceId = account.GetAttributeValue<Guid?>("gc_amadeusintegrationreferenceid");

        // Step 2: Retrieve gc_amadeusintegrationreferenceid for gc_salesinitiativesid
        var salesInitiative = service.Retrieve(gc_salesinitiativesid.LogicalName, gc_salesinitiativesid.Id, new ColumnSet("gc_amadeusintegrationreferenceid"));
        var salesInitiativeIntegrationReferenceId = salesInitiative.GetAttributeValue<Guid?>("gc_amadeusintegrationreferenceid");

        // Check if both integration reference IDs are available
        if (accountIntegrationReferenceId.HasValue && salesInitiativeIntegrationReferenceId.HasValue)
        {
           

            // Trace the values for debugging
            tracingService.Trace("Creating record in custom lookup table for Account Integration Reference: {0}, Sales Initiative Integration Reference: {1}",
                accountIntegrationReferenceId.Value, salesInitiativeIntegrationReferenceId.Value);

            // Set the values for the custom lookup table fields
            salesinitiativeaccountlookup["gc_accountamadeusid"] = accountIntegrationReferenceId.Value; // Use the integration reference ID
            salesinitiativeaccountlookup["gc_salesinitiativeamadeusid"] = salesInitiativeIntegrationReferenceId.Value; // Use the integration reference ID

            try
            {
                // Create the record in the custom lookup table
                service.Create(salesinitiativeaccountlookup);
                tracingService.Trace("Record created successfully.");
            }
            catch (Exception ex)
            {
                tracingService.Trace("Error while creating record: {0}", ex.Message);
                // Retry logic (optional, if necessary)
                System.Threading.Thread.Sleep(2000); // Sleep for 2 seconds (this is just an example)
                try
                {
                    service.Create(salesinitiativeaccountlookup);
                    tracingService.Trace("Record created successfully after retry.");
                }
                catch (Exception retryEx)
                {
                    tracingService.Trace("Error after retry: {0}", retryEx.Message);
                }
            }
        }
        else
        {
            tracingService.Trace("One or both integration reference IDs are not available. Account ID: {0}, Sales Initiative ID: {1}",
                accountid.Id, gc_salesinitiativesid.Id);
        }
    }
    else
    {
        // Disassociation logic: Remove the record from the custom lookup table
        QueryExpression query = new QueryExpression(CustomLookupTableName)
        {
            ColumnSet = new ColumnSet("gc_salesinitiativesaccountlookupid"), // Replace with your custom GUID field
            Criteria = new FilterExpression
            {
                Conditions =
                {
                    new ConditionExpression("gc_accountamadeusid", ConditionOperator.Equal, accountid.Id), // Use the correct field name
                    new ConditionExpression("gc_salesinitiativeamadeusid", ConditionOperator.Equal, gc_salesinitiativesid.Id) // Use the correct field name
                }
            }
        };

        EntityCollection results = service.RetrieveMultiple(query);
        foreach (var entity in results.Entities)
        {
            try
            {
                service.Delete(CustomLookupTableName, entity.Id);
                tracingService.Trace("Record deleted for Account: {0}, Sales Initiative: {1}", accountid.Id, gc_salesinitiativesid.Id);
            }
            catch (Exception ex)
            {
                tracingService.Trace("Error while deleting record: {0}", ex.Message);
            }
        }
    }
}
} */
