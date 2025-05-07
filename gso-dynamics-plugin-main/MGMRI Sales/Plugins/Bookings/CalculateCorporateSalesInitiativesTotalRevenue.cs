using System;
using System.IdentityModel.Metadata;
using System.Runtime.Remoting.Services;
using Helpers;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using static MGMRI_Sales.Utils.BookingUtils;

namespace MGMRI_Sales.Plugins.Bookings
{
    public class CalculateCorporateSalesInitiativesTotalRevenue : IPlugin // CorporateSalesInitiative , CorporateSalesInitiative
    {
        #region variables and constants
        #endregion

        #region Main
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

                // Ids
                Guid bookingId = Guid.Empty;

                // Entity Refs
                EntityReference entCorpSalesInitiative = null;
                Entity entBooking = null;

                // Pre Images to account for deletions and/or removals
                Entity preImageBooking = null;
                EntityReference preImageCorpSalesInitiative = null;
                bool isDefiniteCurrent = false;
                bool isDefinitePreImage = false;

                // Obtain the target entity from the input parameters.
                tracingService.Trace("Acquiring target...");
                entBooking = (Entity)context.InputParameters["Target"];
                bookingId = entBooking.Id;
                tracingService.Trace(bookingId.ToString());

                switch (context.MessageName)
                {
                    case "Create":
                        tracingService.Trace("Create Message received.");
                        entCorpSalesInitiative = ExtractCorpSalesInitiative(entBooking);
                        isDefiniteCurrent = IsStatusDefinite(entBooking);
                        // If its not a definite booking, exit
                        if (!isDefiniteCurrent)
                        {
                            tracingService.Trace("Not a definite booking. No Calc needed");
                            return;
                        }

                        // If no Sales Init, exit
                        if (entCorpSalesInitiative == null)
                        {
                            tracingService.Trace(
                                "Booking not associated with a Corporate Sales Initiative. No Calc needed"
                            );
                            return;
                        }

                        // Otherwise calc
                        tracingService.Trace(
                            $"Sales Init to recalculate Id: {entCorpSalesInitiative.Id}"
                        );
                        getCorpSalesInitiativeRelatedBookings(
                            orgService,
                            tracingService,
                            entCorpSalesInitiative.Id
                        );

                        // We should be done for all Create Messages
                        tracingService.Trace("Calc complete.");
                        return;
                    case "Delete":
                        tracingService.Trace("Delete Message received.");

                        // Get Pre Images
                        if (!context.PreEntityImages.Contains("PreImage_Booking"))
                        {
                            tracingService.Trace("No PreImage. ERROR");
                            throw new Exception("No Pre-Image found");
                        }
                        preImageBooking = (Entity)context.PreEntityImages["PreImage_Booking"];
                        preImageCorpSalesInitiative = ExtractCorpSalesInitiative(preImageBooking);
                        isDefinitePreImage = IsStatusDefinite(preImageBooking);

                        // If it was never definite, no need to recalc.
                        if (!isDefinitePreImage)
                        {
                            tracingService.Trace(
                                "Deleted booking is not a definite booking. No Calc needed"
                            );
                            return;
                        }

                        // If no Sales Init assigned, no need to precalc
                        if (preImageCorpSalesInitiative == null)
                        {
                            tracingService.Trace(
                                "Booking was not associated with a Corporate Sales Initiative. No Calc needed"
                            );
                            return;
                        }

                        // Otherwise calc
                        tracingService.Trace(
                            $"Sales Init to recalculate Id: {preImageCorpSalesInitiative.Id}"
                        );
                        getCorpSalesInitiativeRelatedBookings(
                            orgService,
                            tracingService,
                            preImageCorpSalesInitiative.Id
                        );

                        // We should be done for all Create Messages
                        tracingService.Trace("Calc complete.");
                        return;
                    case "Update":
                        tracingService.Trace("Update Message received.");

                        // Get Pre Images
                        if (!context.PreEntityImages.Contains("PreImage_Booking"))
                        {
                            tracingService.Trace("No PreImage. ERROR");
                            throw new Exception("No Pre-Image found");
                        }
                        preImageBooking = (Entity)context.PreEntityImages["PreImage_Booking"];
                        preImageCorpSalesInitiative = ExtractCorpSalesInitiative(preImageBooking);

                        // Get Post Images and set as current
                        // This is because the current entity will not contain accurate values
                        if (!context.PostEntityImages.Contains("PostImage_Booking"))
                        {
                            tracingService.Trace("No PostImage. ERROR!!");
                            throw new Exception("No PostImage found");
                        }

                        entBooking = (Entity)context.PostEntityImages["PostImage_Booking"];
                        entCorpSalesInitiative = ExtractCorpSalesInitiative(entBooking);
                        break;
                    default:
                        // Do not process anything else.
                        return;
                }

                tracingService.Trace("Calculating if Recalc Needed for: " + entBooking.Id);
                // At this point in time, we know we have has some kind of update.
                bool requiresReCalcOnCurrent = false;
                bool requiresReCalcOnPreImage = false;

                isDefiniteCurrent = IsStatusDefinite(entBooking);
                tracingService.Trace(
                    $"IsDefiniteCurrent {isDefiniteCurrent} - {ExtractStatus(entBooking)}"
                );
                isDefinitePreImage = IsStatusDefinite(preImageBooking);
                tracingService.Trace(
                    $"IsDefinitePreImage {isDefinitePreImage} - {ExtractStatus(preImageBooking)}"
                );

                // First case, the stage changed and one of them is definite.
                if (isDefiniteCurrent != isDefinitePreImage)
                {
                    tracingService.Trace("Status Change. Recalc Required.");
                    requiresReCalcOnCurrent = true;
                    isDefinitePreImage = true;
                }

                // This should not be an else.
                // if the sales init changed, then they may req a recalc
                if (preImageCorpSalesInitiative?.Id != entCorpSalesInitiative?.Id)
                {
                    tracingService.Trace("Sales Init Changed. Recalc Required.");

                    // Recalc current only if its definite.
                    requiresReCalcOnCurrent = isDefiniteCurrent;

                    // Recalc prev only if the prev Image was definite.
                    requiresReCalcOnPreImage = isDefinitePreImage;
                }

                // Last case, one of the major 4 fields changes
                // Not we check for requireReCalc solely to make sure
                // we are not wasting time extracting props if we already plan to recalc
                if (!requiresReCalcOnCurrent && DependentFieldsChanged(entBooking, preImageBooking))
                {
                    tracingService.Trace("Change found on dependent fields. Recalc Needed.");
                    requiresReCalcOnCurrent = true;
                }

                if (entCorpSalesInitiative != null && requiresReCalcOnCurrent)
                {
                    tracingService.Trace("Recalculating current...");
                    getCorpSalesInitiativeRelatedBookings(
                        orgService,
                        tracingService,
                        entCorpSalesInitiative.Id
                    );
                    tracingService.Trace("Calculating current complete");
                }

                if (preImageCorpSalesInitiative != null && requiresReCalcOnPreImage)
                {
                    tracingService.Trace("Recalculating preImage...");
                    getCorpSalesInitiativeRelatedBookings(
                        orgService,
                        tracingService,
                        preImageCorpSalesInitiative.Id
                    );
                    tracingService.Trace("Calculating preImage complete");
                }
            }
            catch (Exception ex)
            {
                throw new InvalidPluginExecutionException(
                    OperationStatus.Failed,
                    message: $"Error: {ex.Message}{Environment.NewLine}StackTrace:{Environment.NewLine}{ex.StackTrace}"
                );
            }
        }

        #endregion


        #region Fetches
        public void getCorpSalesInitiativeRelatedBookings(
            IOrganizationService service,
            ITracingService tracingService,
            Guid corpSalesInitiativeId
        )
        {
            tracingService.Trace("Fetching Corporate Sales Initiatives Related Bookings...");
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
                + "<attribute name='gc_salesinitiative' />\r\n    "
                + "<order attribute='createdon' descending='false' />\r\n    "
                + "<filter type='and'>\r\n      "
                + "   <filter type='or'>\r\n        "
                + "   <condition attribute='gc_corporatesalesinitiative' operator='eq' uiname='CorporateSalesInitiative' uitype='corporatesalesinitiative' value='"
                + corpSalesInitiativeId
                + "' />\r\n        "
                + $"</filter>\r\n      <condition attribute='gc_status' operator='eq' value='{GC_STATUS_DEFINITE_VALUE}' />\r\n      "
                + "<condition attribute='statecode' operator='eq' value='0' />\r\n    "
                + "</filter>\r\n  </entity>\r\n</fetch>";
            EntityCollection ecBookings = service.RetrieveMultiple(
                new FetchExpression(strFetchXML)
            );
            calculateCorpSalesInitiativeRevenue(
                service,
                tracingService,
                corpSalesInitiativeId,
                ecBookings
            );
        }

        #endregion

        #region Calculations
        public void calculateCorpSalesInitiativeRevenue(
            IOrganizationService service,
            ITracingService tracingService,
            Guid corpsalesinitiativeId,
            EntityCollection ecBookings
        )
        {
            tracingService.Trace("Calculating Corporate Sales Initiatives Related Bookings...");

            Decimal totalActualRevenue = 0;
            Decimal totalAgreedGuestroomRevenue = 0;
            Decimal totalFandBMinimum = 0;
            int totalRoomnightsAgreed = 0;
            int totalDefiniteBookingsFound = 0;
            if (ecBookings != null && ecBookings.Entities.Count > 0)
            {
                foreach (Entity entBooking in ecBookings.Entities)
                {
                    if (!IsStatusDefinite(entBooking))
                    {
                        continue;
                    }
                    totalDefiniteBookingsFound++;

                    Money actualEventRev = ExtractMoney_ActualEventRevenueTotal(entBooking);
                    Money agreedGuestRev = ExtractMoney_AgreedGuestRevenueTotal(entBooking);
                    Money fbMin = ExtractMoney_FBMinimum(entBooking);
                    int? totalRoomNights = ExtractInt_TotalRoomNights(entBooking);

                    if (actualEventRev != null)
                        totalActualRevenue += actualEventRev.Value;

                    if (agreedGuestRev != null)
                        totalAgreedGuestroomRevenue += agreedGuestRev.Value;

                    if (fbMin != null)
                        totalFandBMinimum += fbMin.Value;

                    if (totalRoomNights != null)
                        totalRoomnightsAgreed += totalRoomNights.Value;
                }

                // Rounding
                totalActualRevenue = Math.Round(totalActualRevenue, 2);
                totalAgreedGuestroomRevenue = Math.Round(totalAgreedGuestroomRevenue, 2);
                totalFandBMinimum = Math.Round(totalFandBMinimum, 2);

                LogCalculations(
                    tracingService,
                    ecBookings.Entities?.Count ?? 0,
                    totalDefiniteBookingsFound,
                    totalActualRevenue,
                    totalAgreedGuestroomRevenue,
                    totalFandBMinimum,
                    totalRoomnightsAgreed
                );

                updateCorpSalesInitiativeTotalBookingsRevenue(
                    service,
                    corpsalesinitiativeId,
                    totalActualRevenue,
                    totalAgreedGuestroomRevenue,
                    totalFandBMinimum,
                    totalRoomnightsAgreed
                );
            }
            else
            {
                tracingService.Trace("No Corporate Sales Initiatives found.");
                LogCalculations(
                    tracingService,
                    0,
                    0,
                    totalActualRevenue,
                    totalAgreedGuestroomRevenue,
                    totalFandBMinimum,
                    totalRoomnightsAgreed
                );

                updateCorpSalesInitiativeTotalBookingsRevenue(
                    service,
                    corpsalesinitiativeId,
                    totalActualRevenue,
                    totalAgreedGuestroomRevenue,
                    totalFandBMinimum,
                    totalRoomnightsAgreed
                );
            }
        }

        private static void LogCalculations(
            ITracingService tracingService,
            int totalBookingsFound,
            int totalDefiniteBookings,
            decimal totalActualRevenue,
            decimal totalAgreedGuestroomRevenue,
            decimal totalFandBMinimum,
            int totalRoomnightsAgreed
        )
        {
            tracingService.Trace($"Total Bookings Found: {totalBookingsFound}");
            tracingService.Trace($"Total Definite Bookings Found: {totalDefiniteBookings}");
            tracingService.Trace($"Total ActualRevenue: {totalActualRevenue}");
            tracingService.Trace($"TotalAgreedGuestroomRevenue: {totalAgreedGuestroomRevenue}");
            tracingService.Trace($"TotalF&BMinimum: {totalFandBMinimum}");
            tracingService.Trace($"TotalRoomNightsAgreed: {totalRoomnightsAgreed}");
        }

        #endregion

        #region Updates
        public void updateCorpSalesInitiativeTotalBookingsRevenue(
            IOrganizationService service,
            Guid corpsalesinitiativeId,
            Decimal totalActualRevenue,
            Decimal totalAgreedGuestroomRevenue,
            Decimal totalFandBMinimum,
            int totalRoomnightsAgreed
        )
        {
            Entity entCorpSalesInitiative = new Entity(
                "gc_corporatesalesinitiatives",
                corpsalesinitiativeId
            ); //gc_salesinitiatives
            entCorpSalesInitiative["gc_totalactualeventrevenue"] = totalActualRevenue;
            entCorpSalesInitiative["gc_totalagreedguestroomrevenue"] = totalAgreedGuestroomRevenue;
            entCorpSalesInitiative["gc_totalfbminimum"] = totalFandBMinimum;
            entCorpSalesInitiative["gc_totalroomnightsagreed"] = (Decimal)totalRoomnightsAgreed;
            service.Update(entCorpSalesInitiative);
        }
        #endregion
    }
}
