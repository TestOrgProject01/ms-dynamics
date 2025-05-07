using System;
using Helpers;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;

namespace MGMRI_Sales.Plugins.Bookings
{
    public class CalculateAccountsTotalRevenue : IPlugin
    {
        public void Execute(IServiceProvider serviceProvider)
        {
            try
            {
                // Services
                IPluginExecutionContext context = (IPluginExecutionContext)
                    serviceProvider.GetService(typeof(IPluginExecutionContext));
                IOrganizationServiceFactory serviceFactory = (IOrganizationServiceFactory)
                    serviceProvider.GetService(typeof(IOrganizationServiceFactory));
                IOrganizationService orgService = serviceFactory.CreateOrganizationService(
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

                if (!context.Stage.Equals(40))
                {
                    tracingService.Log("This is not a Post Op. Exiting...");
                    return;
                }

                // Switch
                switch (context.MessageName.ToUpper())
                {
                    case "CREATE":
                        tracingService.Trace("Message Name " + context.MessageName);
                        ProcessMessage_Create(context, orgService, tracingService);
                        tracingService.LogComplete();
                        return;
                    case "UPDATE":
                        tracingService.Trace("Message Name " + context.MessageName);
                        ProcessMessage_Update(context, orgService, tracingService);
                        tracingService.LogComplete();
                        return;
                    default:
                        tracingService.LogUnsupportedMessageType(context.MessageName);
                        tracingService.LogComplete();
                        return;
                }
            }
            catch (Exception ex)
            {
                throw new InvalidPluginExecutionException("Error", ex);
            }
        }

        private void ProcessMessage_Update(
            IPluginExecutionContext context,
            IOrganizationService orgService,
            ITracingService tracingService
        )
        {
            // Vars
            Entity entAccount = null;
            Entity entAgency = null;
            Entity preImageAccount = null;
            Entity preImageAgency = null;
            Entity entBooking = (Entity)context.InputParameters["Target"];

            if (entBooking == null || entBooking.Id == Guid.Empty)
            {
                tracingService.Trace("Invalid Booking Id");
                return;
            }

            tracingService.Trace($"Booking Id: {entBooking.Id}");

            // Get PreImage
            Entity preImageBooking =
                context.GetPreImage("PreImage_Booking", tracingService) as Entity;

            if (preImageBooking == null)
            {
                tracingService.Trace("No change registered...");
                return;
            }

            // bool fieldChanged = false;
            // foreach (var attribute in entBooking.Attributes)
            // {
            //     if (preImageBooking.Contains(attribute.Key))
            //     {
            //         var oldValue = preImageBooking[attribute.Key];
            //         var newValue = entBooking[attribute.Key];

            //         if (!oldValue.Equals(newValue))
            //         {
            //             // Field has changed
            //             Console.WriteLine($"Field {attribute.Key} changed from {oldValue} to {newValue}");
            //             fieldChanged = true;
            //         }
            //     }
            // }

            if (preImageBooking.Contains("gc_account"))
            {
                tracingService.Trace("Found Pre Image Booking with gc_Account");
                var entRef = (EntityReference)preImageBooking["gc_account"];
                if (entRef != null)
                {
                    preImageAccount = Account.GetAccountWithRollupFields(orgService, entRef);
                }
            }

            if (preImageBooking.Contains("gc_agency"))
            {
                tracingService.Trace("Found Pre Image Booking with gc_Agency");
                var entRef = (EntityReference)preImageBooking["gc_agency"];
                if (entRef != null)
                {
                    preImageAgency = Account.GetAccountWithRollupFields(orgService, entRef);
                }
            }

            // Retrieving because local copy may have incomplete picture.
            tracingService.Trace($"Retreiving booking with id: {entBooking.Id}");
            Entity retrievedBookingRecord = orgService.Retrieve(
                "gc_booking",
                entBooking.Id,
                new ColumnSet("gc_account", "gc_agency")
            );

            tracingService.Trace("Retrieved Record " + retrievedBookingRecord.Id);

            if (retrievedBookingRecord == null)
            {
                tracingService.Trace("No Booking retrieved. Potential error? Exiting...");
                // Do we continue. Not sure.
                return;
            }
            if (retrievedBookingRecord.Contains("gc_account"))
            {
                var entRef = (EntityReference)retrievedBookingRecord["gc_account"];
                if (entRef != null)
                {
                    entAccount = Account.GetAccountWithRollupFields(orgService, entRef);
                }
            }
            if (retrievedBookingRecord.Contains("gc_agency"))
            {
                var entRef = (EntityReference)retrievedBookingRecord["gc_agency"];
                if (entRef != null)
                {
                    entAgency = Account.GetAccountWithRollupFields(orgService, entRef);
                }
            }
            CalculateOnUpdate(
                orgService,
                tracingService,
                entBooking,
                entAccount,
                entAgency,
                preImageAccount,
                preImageAgency
            );
        }

        private void ProcessMessage_Create(
            IPluginExecutionContext context,
            IOrganizationService orgService,
            ITracingService tracingService
        )
        {
            Guid bookingId = Guid.Empty;
            Entity entAccount = null;
            Entity entAgency = null;
            Entity entBooking = (Entity)context.InputParameters["Target"];

            // Get assocaited entities
            if (entBooking.Contains("gc_account"))
            {
                var entRef = (EntityReference)entBooking["gc_account"];
                if (entRef != null)
                {
                    entAccount = Account.GetAccountWithRollupFields(orgService, entRef);
                }

                tracingService.Trace("Found an Account Entity on the booking.");
            }
            if (entBooking.Contains("gc_agency"))
            {
                var entRef = (EntityReference)entBooking["gc_agency"];
                if (entRef != null)
                {
                    entAgency = Account.GetAccountWithRollupFields(orgService, entRef);
                }
                tracingService.Trace("Found an Agency Entity on the booking.");
            }

            // Calculate
            CalculateOnCreate(orgService, tracingService, entBooking, entAccount, entAgency);
        }

        private void CalculateOnCreate(
            IOrganizationService orgService,
            ITracingService tracingService,
            Entity booking,
            Entity entAccount,
            Entity entAgency
        )
        {
            Guid accId = entAccount?.Id ?? Guid.Empty;
            Guid agencyId = entAgency?.Id ?? Guid.Empty;

            if (accId == Guid.Empty && agencyId == Guid.Empty)
            {
                tracingService.Log("Account and agency  null. No further processing needed...");
                return;
            }

            // This should never happen in the UI??
            // But just in case.
            if (accId == agencyId)
            {
                // Just calc one?
                // This may be a bug.
                tracingService.Log("Account and Agency are identical. Calculating one...");
                GetAccountRelatedBookings(orgService, tracingService, entAccount);
                tracingService.Log(
                    @"If numbers look inaccurate, contact Warda with the following:
                CalcAccountsTotalRevenue -> CalcOnCreate -> 148"
                );
                return;
            }

            tracingService.Log("Account and Agency are NOT identical.");
            if (agencyId != Guid.Empty)
            {
                tracingService.Log("Agency was not null. Calculating...");
                GetAccountRelatedBookings(orgService, tracingService, entAgency);
            }
            if (accId != Guid.Empty)
            {
                tracingService.Log("Account was not null. Calculating...");
                GetAccountRelatedBookings(orgService, tracingService, entAccount);
            }
            tracingService.Log("Calculation On Create complete.");
        }

        private bool CalculateOnUpdate(
            IOrganizationService orgService,
            ITracingService tracingService,
            Entity booking,
            Entity entAccount,
            Entity entAgency,
            Entity preImageAccount,
            Entity preImageAgency
        )
        {
            // Note booking Id can come in null here. But that's okay.
            bool accountChangedHandled = false;
            bool agencyChangeHandled = false;

            // Nothing changed. Dont recalc.
            if (entAccount?.Id == preImageAccount?.Id && entAgency?.Id == preImageAgency?.Id)
            {
                tracingService.Trace("No change on Account or Agency. No calc needed.");
                return false;
            }

            // Account field was modified...
            if (entAccount?.Id != preImageAccount?.Id)
            {
                tracingService.Trace("Change on Account detected...");
                if (entAccount?.Id != null)
                {
                    tracingService.Trace("New Account assigned. Recalc...");
                    GetAccountRelatedBookings(orgService, tracingService, entAccount);
                }

                if (preImageAccount != null)
                {
                    tracingService.Trace("Old Account was removed. Recalc...");
                    GetAccountRelatedBookingsDissociate(
                        orgService,
                        tracingService,
                        preImageAccount,
                        booking?.Id ?? Guid.Empty
                    );
                }
                accountChangedHandled = true;
            }

            // Agency field was modified...
            if (entAgency?.Id != preImageAgency?.Id)
            {
                tracingService.Trace("Change on Agency detected...");
                if (entAgency?.Id != null)
                {
                    tracingService.Trace("New Agency assigned. Recalc...");
                    GetAccountRelatedBookings(orgService, tracingService, entAgency);
                }

                if (preImageAgency != null)
                {
                    tracingService.Trace("Old Agency was removed. Recalc...");
                    GetAccountRelatedBookingsDissociate(
                        orgService,
                        tracingService,
                        preImageAgency,
                        booking?.Id ?? Guid.Empty
                    );
                }
                agencyChangeHandled = true;
            }

            return accountChangedHandled || agencyChangeHandled;
        }

        public void GetAccountRelatedBookings(
            IOrganizationService service,
            ITracingService tracingService,
            Entity account
        )
        {
            tracingService.Trace("Fetching Account Related Bookings...");
            tracingService.Trace($"Account Id: {account.Id}");
            var strFetchXML =
                "<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>\r\n  "
                + "<entity name='gc_booking'>\r\n    "
                + "<attribute name='gc_bookingid' />\r\n    "
                + "<attribute name='createdon' />\r\n    "
                + "<attribute name='gc_totalroomnightsagreed' />\r\n    "
                + "<attribute name='gc_status' />\r\n    "
                + "<attribute name='gc_fbminimum' />\r\n    "
                + "<attribute name='gc_agreedguestroomrevenuetotal' />\r\n    "
                + "<attribute name='gc_agency' />\r\n    "
                + "<attribute name='gc_actualeventrevenuetotal' />\r\n    "
                + "<attribute name='gc_account' />\r\n    "
                + "<order attribute='createdon' descending='false' />\r\n    "
                + "<filter type='and'>\r\n      "
                + "   <filter type='or'>\r\n        "
                + "   <condition attribute='gc_account' operator='eq' uiname='Account ABC' uitype='account' value='"
                + account.Id
                + "' />\r\n        "
                + "   <condition attribute='gc_agency' operator='eq' uiname='Account ABC' uitype='account' value='"
                + account.Id
                + "' />\r\n      "
                + "</filter>\r\n      <condition attribute='gc_status' operator='eq' value='122680002' />\r\n      "
                + "<condition attribute='statecode' operator='eq' value='0' />\r\n    "
                + "</filter>\r\n  </entity>\r\n</fetch>";

            tracingService.Trace($"FetchXML:{Environment.NewLine} {strFetchXML}");

            EntityCollection ecBookings = service.RetrieveMultiple(
                new FetchExpression(strFetchXML)
            );

            tracingService.Trace($"Successfully retrieved Bookings. Count: {ecBookings.TotalRecordCount}");
            Account.CalculateAccountRevenue(service, tracingService, account, ecBookings);
        }

        public void GetAccountRelatedBookingsDissociate(
            IOrganizationService service,
            ITracingService tracingService,
            Entity account,
            Guid bookingId
        )
        {
            tracingService.Trace("Fetching Account Related Bookings Dissoaciate...");
            tracingService.Trace($"Account Id: {account.Id}");
            var strFetchXML =
                @"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                  <entity name='gc_booking'>
                                    <attribute name='gc_bookingid' />
                                    <attribute name='gc_bookingpostas' />
                                    <attribute name='gc_actualeventrevenuetotal' />
                                    <attribute name='gc_agreedguestroomrevenuetotal' />
                                    <attribute name='gc_fbminimum' />
                                    <attribute name='gc_totalroomnightsagreed' />
                                    <attribute name='createdon' />
                                    <order attribute='gc_bookingpostas' descending='false' />
                                    <filter type='and'>
                                      <condition attribute='gc_status' operator='eq' value='122680002' />
                                      <condition attribute='statuscode' operator='eq' value='1' />
                                      <filter type='or'>
                                        <condition attribute='gc_account' operator='eq' uitype='account' value='"
                + account.Id
                + @"' />
                                        <condition attribute='gc_agency' operator='eq' uitype='account' value='"
                + account.Id
                + @"' />
                                      </filter>
                                      <condition attribute='gc_bookingid' operator='ne' uitype='gc_booking' value='"
                + bookingId
                + @"' />
                                    </filter>
                                  </entity>
                                </fetch>";

            tracingService.Trace($"FetchXML:{Environment.NewLine} {strFetchXML}");
            EntityCollection ecBookings = service.RetrieveMultiple(
                new FetchExpression(strFetchXML)
            );
            tracingService.Trace($"Successfully retrieved Bookings. Count: {ecBookings.TotalRecordCount}");
            Account.CalculateAccountRevenue(service, tracingService, account, ecBookings);
        }
    }
}
