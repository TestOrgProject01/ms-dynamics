using System;
using System.Security.Principal;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;

namespace MGMRI_Sales.Utils
{
    public static class BookingUtils
    {
        public const int GC_STATUS_DEFINITE_VALUE = 122680002;

        #region Field Extractors
        public static bool DependentFieldsChanged(Entity entBooking, Entity preImageBooking)
        {
            if (
                ExtractMoney_ActualEventRevenueTotal(entBooking)
                != ExtractMoney_ActualEventRevenueTotal(preImageBooking)
            )
            {
                return true;
            }
            if (
                ExtractMoney_ActualEventRevenueTotal(entBooking)
                != ExtractMoney_ActualEventRevenueTotal(preImageBooking)
            )
            {
                return true;
            }
            if (
                ExtractMoney_AgreedGuestRevenueTotal(entBooking)
                != ExtractMoney_AgreedGuestRevenueTotal(preImageBooking)
            )
            {
                return true;
            }
            if (
                ExtractInt_TotalRoomNights(entBooking)
                != ExtractInt_TotalRoomNights(preImageBooking)
            )
            {
                return true;
            }
            return false;
        }

        public static Money ExtractMoney_ActualEventRevenueTotal(Entity entBooking) =>
            ExtractField_Money(entBooking, "gc_actualeventrevenuetotal");

        public static Money ExtractMoney_AgreedGuestRevenueTotal(Entity entBooking) =>
            ExtractField_Money(entBooking, "gc_agreedguestroomrevenuetotal");

        public static Money ExtractMoney_FBMinimum(Entity entBooking) =>
            ExtractField_Money(entBooking, "gc_fbminimum");

        public static int? ExtractInt_TotalRoomNights(Entity entBooking) =>
            ExtractField_Int(entBooking, "gc_totalroomnightsagreed");

        public static EntityReference ExtractSalesInitiative(Entity entBooking) =>
            ExtractField_EntityRef(entBooking, "gc_salesinitiative");

        public static EntityReference ExtractCorpSalesInitiative(Entity entBooking) =>
            ExtractField_EntityRef(entBooking, "gc_corporatesalesinitiative");

        public static bool IsStatusDefinite(Entity entity) =>
            ExtractStatus(entity) == GC_STATUS_DEFINITE_VALUE;

        public static int? ExtractStatus(Entity entity)
        {
            var field = "gc_status";
            if (entity == null)
            {
                return -1;
            }
            var val = entity.Contains(field) ? (OptionSetValue)entity[field] : null;
            return val?.Value;
        }

        public static EntityReference ExtractField_EntityRef(Entity entity, string field)
        {
            if (entity == null)
            {
                return null;
            }
            return entity.Contains(field) ? (EntityReference)entity[field] : null;
        }

        public static Money ExtractField_Money(Entity entity, string field)
        {
            if (entity == null)
            {
                return null;
            }
            return entity.Contains(field) ? (Money)entity[field] : default;
        }

        public static int? ExtractField_Int(Entity entity, string field)
        {
            if (entity == null)
            {
                return null;
            }
            return entity.Contains(field) ? (int)entity[field] : (int?)null;
        }

        #endregion
    }
}
