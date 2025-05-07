using System;
using System.Security.Principal;
using Helpers;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;

namespace MGMRI_Sales.Plugins.Bookings
{
    public class CreateThirdPartyAgencyPlugin : IPlugin
    {
        public void Execute(IServiceProvider serviceProvider)
        {
            try
            {
                IPluginExecutionContext context = (IPluginExecutionContext)
                    serviceProvider.GetService(typeof(IPluginExecutionContext));
                IOrganizationServiceFactory serviceFactory = (IOrganizationServiceFactory)
                    serviceProvider.GetService(typeof(IOrganizationServiceFactory));
                IOrganizationService orgService = serviceFactory.CreateOrganizationService(
                    context.UserId
                );
                ITracingService tracingService = (ITracingService)
                    serviceProvider.GetService(typeof(ITracingService));

                Guid accId = Guid.Empty;
                Guid agencyId = Guid.Empty;
                Guid bookingId = Guid.Empty;
                EntityReference entAccount = null;
                EntityReference entAgency = null;
                bool bDuplicateFlag = false;
                Entity entBooking = null;
                int bookingStatus;

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
                entBooking = (Entity)context.InputParameters["Target"];
                bookingId = entBooking.Id;
                Entity retrievedBookingRecord = orgService.Retrieve(
                    "gc_booking",
                    bookingId,
                    new ColumnSet("gc_account", "gc_agency", "gc_status")
                );
                tracingService.Trace("retrieveRecord " + retrievedBookingRecord.Id);
                if (
                    retrievedBookingRecord != null
                    && retrievedBookingRecord.Contains("gc_account")
                    && retrievedBookingRecord.Contains("gc_agency")
                    && retrievedBookingRecord.Contains("gc_status")
                )
                {
                    tracingService.Trace("Processing...");
                    bookingStatus = ((OptionSetValue)retrievedBookingRecord["gc_status"]).Value;
                    entAccount = (EntityReference)retrievedBookingRecord["gc_account"];
                    entAgency = (EntityReference)retrievedBookingRecord["gc_agency"];

                    if (
                        bookingStatus == 122680002
                        && entAccount != null
                        && entAgency != null
                        && entAccount.Id != Guid.Empty
                        && entAgency.Id != Guid.Empty
                    )
                    {
                        accId = entAccount.Id;
                        agencyId = entAgency.Id;
                        bDuplicateFlag = checkDuplicateRecords(
                            accId,
                            agencyId,
                            orgService,
                            tracingService
                        );
                        tracingService.Trace("Flag : " + bDuplicateFlag);
                        if (!bDuplicateFlag)
                        {
                            CreateThirdPartyAgencyRecord(orgService, accId, agencyId);
                        }
                    }
                    tracingService.LogComplete();
                    return;
                }
                tracingService.Trace("Missing important fields. Cannot calculate further... ");
                tracingService.LogComplete();
                return;
            }
            catch (Exception ex)
            {
                throw new InvalidPluginExecutionException("Error", ex);
            }
        }

        public bool checkDuplicateRecords(
            Guid accId,
            Guid agencyId,
            IOrganizationService service,
            ITracingService trace
        )
        {
            Entity entTPA = new Entity("gc_thirdpartyagency");
            QueryExpression query = new QueryExpression("gc_thirdpartyagency");
            query.ColumnSet.AddColumn("gc_name");
            query.Criteria = new FilterExpression();

            FilterExpression childFilter = query.Criteria.AddFilter(LogicalOperator.And);
            childFilter.AddCondition("gc_accountid", ConditionOperator.Equal, accId);
            childFilter.AddCondition("gc_agencyid", ConditionOperator.Equal, agencyId);
            EntityCollection ec = service.RetrieveMultiple(query);
            trace.Trace("EC count : " + ec.Entities.Count.ToString());
            if (ec.Entities.Count > 0)
                return true;
            else
                return false;
        }

        public void CreateThirdPartyAgencyRecord(
            IOrganizationService service,
            Guid accId,
            Guid agencyId
        )
        {
            Entity tpa = new Entity("gc_thirdpartyagency");
            tpa["gc_name"] = "AccountName_AgencyName";
            tpa["gc_accountid"] = new EntityReference("account", accId);
            tpa["gc_agencyid"] = new EntityReference("account", agencyId);
            service.Create(tpa);
        }
    }
}
