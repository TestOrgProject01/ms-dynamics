using System;
using Helpers;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;

namespace MGMRI_Sales.Plugins.Bookings
{
    public class CalculateAccountsTotalRevenueOnDelete : IPlugin
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

                if (!context.Stage.Equals(20))
                {
                    tracingService.Log("This is not a Pre Op. Exiting...");
                    return;
                }

                // Switch
                switch (context.MessageName.ToUpper())
                {
                    case "DELETE":
                        tracingService.Trace("Message Name " + context.MessageName);
                        ProcessMessage_Delete(context, orgService, tracingService);
                        return;
                    default:
                        tracingService.LogUnsupportedMessageType(context.MessageName);
                        tracingService.LogComplete();
                        break;
                }
            }
            catch (Exception ex)
            {
                throw new InvalidPluginExecutionException("Error", ex);
            }
        }

        private bool ProcessMessage_Delete(
            IPluginExecutionContext context,
            IOrganizationService orgService,
            ITracingService tracingService
        )
        {
            Guid accId = Guid.Empty;
            Guid agencyId = Guid.Empty;
            Entity entAccount = null;
            Entity entAgency = null;
            Entity entBooking = (Entity)context.InputParameters["Target"];
            Entity preImageBooking = null;
            int bookingStatus;

            // Get Pre Image
            preImageBooking = (Entity)context.GetPreImage("PreImage_Booking", tracingService);
            tracingService.Trace("PreImage Retrieved Record " + preImageBooking?.Id);

            if (preImageBooking != null)
            {
                if (preImageBooking.Contains("gc_status"))
                {
                    bookingStatus = ((OptionSetValue)preImageBooking["gc_status"]).Value;
                    tracingService.Trace("Booking Status " + bookingStatus);
                    if (bookingStatus != 122680002)
                    {
                        return false;
                    }
                    if (preImageBooking.Contains("gc_account"))
                    {
                        var entRef = (EntityReference)preImageBooking["gc_account"];
                        if (entRef != null)
                        {
                            entAccount = Account.GetAccountWithRollupFields(orgService, entRef);
                        }
                        tracingService.Trace("Found an Account Entity on the booking.");
                    }
                    if (preImageBooking.Contains("gc_agency"))
                    {
                        var entRef = (EntityReference)preImageBooking["gc_agency"];
                        if (entRef != null)
                        {
                            entAgency = Account.GetAccountWithRollupFields(orgService, entRef);
                        }
                        tracingService.Trace("Found an Agency Entity on the booking.");
                        tracingService.Trace("Pre Image Agency " + entAgency.Id);
                    }
                }
            }

            accId = entAccount?.Id ?? Guid.Empty;
            agencyId = entAgency?.Id ?? Guid.Empty;

            if (accId == agencyId && accId == Guid.Empty)
            {
                // Empty. Easy exit.
                tracingService.Trace(
                    "Null Agency and Account Entity. No further processing needed..."
                );
                return false;
            }

            if (accId != Guid.Empty)
            {
                GetAccountRelatedDefiniteBookingsOnDelete(
                    orgService,
                    tracingService,
                    entAccount,
                    entBooking
                );
            }
            if (agencyId != Guid.Empty)
            {
                GetAccountRelatedDefiniteBookingsOnDelete(
                    orgService,
                    tracingService,
                    entAgency,
                    entBooking
                );
            }

            return true;
        }

        public void GetAccountRelatedDefiniteBookingsOnDelete(
            IOrganizationService service,
            ITracingService tracingService,
            Entity account,
            Entity bookingId
        )
        {
            tracingService.Trace("Inside get related bookings ");
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
                    + account?.Id
                ?? Guid.Empty
                    + @"' />
                                        <condition attribute='gc_agency' operator='eq' uitype='account' value='"
                    + account?.Id
                ?? Guid.Empty
                    + @"' />
                                      </filter>
                                      <condition attribute='gc_bookingid' operator='ne' uitype='gc_booking' value='"
                    + bookingId?.Id
                ?? Guid.Empty
                    + @"' />
                                    </filter>
                                  </entity>
                                </fetch>";
            EntityCollection ecBookings = service.RetrieveMultiple(
                new FetchExpression(strFetchXML)
            );
            Account.CalculateAccountRevenue(service, tracingService, account, ecBookings);
        }
    }
}
