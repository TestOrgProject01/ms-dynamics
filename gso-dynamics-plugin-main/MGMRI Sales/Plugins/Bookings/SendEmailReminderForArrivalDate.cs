using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Metadata;
using Microsoft.Xrm.Sdk.Query;

namespace MGMRI_Sales.Plugins.Bookings
{
    public class SendEmailReminderForArrivalDate : IPlugin
    {
        public static string strDefiniteBookingsFetchXml;

        public void Execute(IServiceProvider serviceProvider)
        {
            try
            {
                // This is doing nothing??
                IPluginExecutionContext context = (IPluginExecutionContext)
                    serviceProvider.GetService(typeof(IPluginExecutionContext));
                ITracingService tracingService = (ITracingService)
                    serviceProvider.GetService(typeof(ITracingService));
                IOrganizationServiceFactory serviceFactory = (IOrganizationServiceFactory)
                    serviceProvider.GetService(typeof(IOrganizationServiceFactory));
                IOrganizationService service = serviceFactory.CreateOrganizationService(
                    context.UserId
                );

                tracingService.Trace("Empty Plugin. Please unregister!!");
                // Log start
                tracingService.LogStart();
                tracingService.LogEntryPoint(context);
                tracingService.LogComplete();
            }
            catch (InvalidPluginExecutionException ex)
            {
                throw new InvalidPluginExecutionException("Error " + ex.Message);
            }
        }

        public EntityCollection retrieveBookingsWithDefiniteStatus(IOrganizationService service)
        {
            strDefiniteBookingsFetchXml =
                @"<fetch version='1.0' output-format='xml-platform' mapping='logical' distinct='false'>
                                                                      <entity name='gc_booking'>
                                                                        <attribute name='gc_bookingid' />
                                                                        <attribute name='gc_bookingpostas' />
                                                                        <attribute name='createdon' />
                                                                        <attribute name='statecode' />
                                                                        <attribute name='gc_status' />
                                                                        <attribute name='gc_contact' />
                                                                        <attribute name='gc_arrival' />
                                                                        <attribute name='gc_agency' />
                                                                        <attribute name='gc_account' />
                                                                        <attribute name='mgm_origin_txt' />
                                                                        <order attribute='gc_bookingpostas' descending='false' />
                                                                        <filter type='and'>
                                                                          <condition attribute='gc_status' operator='eq' value='122680002' />
                                                                          <condition attribute='statecode' operator='eq' value='0' />
	                                                                      <filter type='and'>
                                                                              <condition attribute='mgm_origin_txt' operator='not-null' />
                                                                              <condition attribute='mgm_origin_txt' operator='eq' value='GSO' />	                                                                      
                                                                        </filter>
                                                                        </filter>
                                                                        <link-entity name='account' from='accountid' to='gc_account' link-type='inner' alias='ac'>
                                                                          <attribute name='gc_secondarygso' />
                                                                          <attribute name='gc_primarygso' />
                                                                          <filter type='and'>
                                                                            <filter type='or'>
                                                                              <condition attribute='gc_primarygso' operator='not-null' />
                                                                              <condition attribute='gc_secondarygso' operator='not-null' />
                                                                            </filter>
                                                                          </filter>
                                                                        </link-entity>
                                                                      </entity>
                                                                    </fetch>";

            EntityCollection ecBookings = service.RetrieveMultiple(
                new FetchExpression(String.Format(strDefiniteBookingsFetchXml))
            );
            return ecBookings;
        }
    }
}
