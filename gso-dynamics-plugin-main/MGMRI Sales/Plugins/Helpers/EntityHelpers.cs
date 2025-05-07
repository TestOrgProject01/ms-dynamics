using System;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;

namespace Helpers
{
    public static class Account
    {
        public const string Field_TotalActualRevenue = "gc_totalactualeventrevenue";
        public const string Field_TotalAgreedGuestroomRevenue = "gc_totalagreedguestroomrevenue";
        public const string Field_TotalFBMin = "gc_totalfbminimum";
        public const string Field_TotalRoomnightsAgreed = "gc_totalroomnightsagreed";

        public static Entity GetAccountWithRollupFields(
            IOrganizationService orgService,
            EntityReference entRef
        )
        {
            return orgService.Retrieve(
                "account",
                entRef.Id,
                new ColumnSet(
                    Field_TotalActualRevenue,
                    Field_TotalAgreedGuestroomRevenue,
                    Field_TotalFBMin,
                    Field_TotalRoomnightsAgreed
                )
            );
        }

        public static void UpdateAccountTotalBookingsRevenue(
            IOrganizationService service,
            Guid accountId,
            Decimal totalActualRevenue,
            Decimal totalAgreedGuestroomRevenue,
            Decimal totalFandBMinimum,
            int totalRoomnightsAgreed
        )
        {
            Entity entAccount = new Entity("account", accountId);
            entAccount[Field_TotalActualRevenue] = totalActualRevenue;
            entAccount[Field_TotalAgreedGuestroomRevenue] = totalAgreedGuestroomRevenue;
            entAccount[Field_TotalFBMin] = totalFandBMinimum;
            entAccount[Field_TotalRoomnightsAgreed] = (Decimal)totalRoomnightsAgreed;
            service.Update(entAccount);
        }

        public static void CalculateAccountRevenue(
            IOrganizationService service,
            ITracingService tracingService,
            Entity account,
            EntityCollection ecBookings
        )
        {
            tracingService.Trace("Calculating Account Revenue...");
            Decimal new_totalActualRevenue = 0;
            Decimal new_totalAgreedGuestroomRevenue = 0;
            Decimal new_totalFandBMinimum = 0;
            int new_totalRoomnightsAgreed = 0;

            tracingService.Trace("Retrieving old values from account...");

            foreach (var attr in account.Attributes)
            {
                tracingService.Trace($"Found Attribute: {attr.Key} - {attr.Value}");
            }

            decimal old_totalActualRevenue = account.Attributes.Contains(
                Field_TotalActualRevenue
            )
                ? ((Money)account.Attributes[Field_TotalActualRevenue]).Value
                : 0;
            tracingService.Trace($"Test 1: {old_totalActualRevenue}");

            decimal old_totalAgreedGuestroomRevenue = account.Attributes.Contains(
                Field_TotalAgreedGuestroomRevenue
            )
                ? ((Money)account.Attributes[Field_TotalAgreedGuestroomRevenue]).Value
                : 0;
            tracingService.Trace($"Test 2: {old_totalAgreedGuestroomRevenue}");

            decimal old_totalFandBMinimum = account.Attributes.Contains(Field_TotalFBMin)
                ? ((Money)account.Attributes[Field_TotalFBMin]).Value
                : 0;
            tracingService.Trace($"Test 3: {old_totalFandBMinimum}");

            int old_totalRoomnightsAgreed = account.Attributes.Contains(Field_TotalRoomnightsAgreed)
                ? (int)account.Attributes[Field_TotalRoomnightsAgreed]
                : 0;
            tracingService.Trace($"Test 4: {old_totalRoomnightsAgreed}");

            tracingService.Trace("Calculating new values...");

            if (ecBookings != null && ecBookings.Entities.Count > 0)
            {
                tracingService.Trace("Calculating from found bookings...");
                foreach (Entity entBooking in ecBookings.Entities)
                {
                    if (entBooking.Contains("gc_actualeventrevenuetotal"))
                        new_totalActualRevenue = Math.Round(
                            new_totalActualRevenue
                                + ((Money)entBooking["gc_actualeventrevenuetotal"]).Value,
                            2
                        );
                    if (entBooking.Contains("gc_agreedguestroomrevenuetotal"))
                        new_totalAgreedGuestroomRevenue = Math.Round(
                            new_totalAgreedGuestroomRevenue
                                + ((Money)entBooking["gc_agreedguestroomrevenuetotal"]).Value,
                            2
                        );
                    if (entBooking.Contains("gc_fbminimum"))
                        new_totalFandBMinimum = Math.Round(
                            new_totalFandBMinimum + ((Money)entBooking["gc_fbminimum"]).Value,
                            2
                        );
                    if (entBooking.Contains(Field_TotalRoomnightsAgreed))
                        new_totalRoomnightsAgreed =
                            new_totalRoomnightsAgreed
                            + (int)entBooking[Field_TotalRoomnightsAgreed];
                }
            }

            tracingService.Trace($"OLD: Total Account Revenue {old_totalActualRevenue}");
            tracingService.Trace($"NEW: Total Account Revenue {new_totalActualRevenue}");
            tracingService.Trace($"OLD: Total Actual Revenue {old_totalActualRevenue}");
            tracingService.Trace($"NEW: Total Actual Revenue {new_totalActualRevenue}");
            tracingService.Trace(
                $"OLD: Total Agreed Guestroom Revenue {old_totalAgreedGuestroomRevenue}"
            );
            tracingService.Trace(
                $"NEW: Total Agreed Guestroom Revenue {new_totalAgreedGuestroomRevenue}"
            );
            tracingService.Trace($"OLD: Total F&B Minimum {old_totalFandBMinimum}");
            tracingService.Trace($"NEW: Total F&B Minimum {new_totalFandBMinimum}");
            tracingService.Trace($"OLD: Total RoomNights Agreed {old_totalRoomnightsAgreed}");
            tracingService.Trace($"NEW: Total RoomNights Agreed {new_totalRoomnightsAgreed}");

            bool updateNeeded =
                new_totalActualRevenue != old_totalActualRevenue
                || new_totalAgreedGuestroomRevenue != old_totalAgreedGuestroomRevenue
                || new_totalFandBMinimum != old_totalFandBMinimum
                || new_totalRoomnightsAgreed != old_totalRoomnightsAgreed;

            if (updateNeeded)
            {
                tracingService.Trace($"Calculation change detected. Updating Account entity...");
                UpdateAccountTotalBookingsRevenue(
                    service,
                    account.Id,
                    new_totalActualRevenue,
                    new_totalAgreedGuestroomRevenue,
                    new_totalFandBMinimum,
                    new_totalRoomnightsAgreed
                );
                return;
            }
            tracingService.Trace($"No change detected. No update needed.");
        }
    }
}
