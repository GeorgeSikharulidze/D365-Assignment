using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Organization;
using Microsoft.Xrm.Sdk.Query;
using System;
using System.Collections.Generic;
using System.Linq;

namespace GS.D365.Plugins
{
    public class AccountAddressSyncPlugin : IPlugin
    {
        private const string ACCOUNT_ENTITY = "account";
        private const string CONTACT_ENTITY = "contact";
        private const string CUSTOM_ADDRESS_FIELD = "gs_customaccountaddress";
        private const string CONTACT_PARENT_FIELD = "parentcustomerid";


        public void Execute(IServiceProvider serviceProvider)
        {
            ITracingService tracingService = (ITracingService)serviceProvider.GetService(typeof(ITracingService));

            IPluginExecutionContext context = (IPluginExecutionContext)serviceProvider.GetService(typeof(IPluginExecutionContext));

            IOrganizationServiceFactory serviceFactory = (IOrganizationServiceFactory)serviceProvider.GetService(typeof(IOrganizationServiceFactory));
            IOrganizationService service = serviceFactory.CreateOrganizationService(context.UserId);

            try
            {
                tracingService.Trace("AccountAddressSyncPlugin: Started execution");

                if (context.MessageName.ToLower() != "update")
                {
                    tracingService.Trace("Plugin triggered on non-update message. Exiting.");
                    return;
                }

                if (!context.InputParameters.Contains("Target") || !(context.InputParameters["Target"] is Entity))
                {
                    tracingService.Trace("No Target entity found. Exiting.");
                    return;
                }

                Entity targetAccount = (Entity)context.InputParameters["Target"];

                if (targetAccount.LogicalName != ACCOUNT_ENTITY)
                {
                    tracingService.Trace($"Target entity is {targetAccount.LogicalName}, not account. Exiting.");
                    return;
                }

                tracingService.Trace($"Processing Account: {targetAccount.Id}");

                if (!targetAccount.Contains(CUSTOM_ADDRESS_FIELD))
                {
                    tracingService.Trace("CustomAccountAddress not in update payload. Exiting.");
                    return;
                }

                string newAddress = targetAccount.GetAttributeValue<string>(CUSTOM_ADDRESS_FIELD);
                tracingService.Trace($"New CustomAccountAddress value: {newAddress ?? "NULL"}");

                List<Entity> relatedContacts = GetRelatedContacts(service, targetAccount.Id, tracingService);
                tracingService.Trace($"Found {relatedContacts.Count} related contacts");

                int updatedCount = 0;
                int errorCount = 0;

                foreach (Entity contact in relatedContacts)
                {
                    try
                    {
                        UpdateContactAddress(service, contact.Id, newAddress, tracingService);
                        updatedCount++;
                    }
                    catch (Exception ex)
                    {
                        errorCount++;
                        tracingService.Trace($"Error updating contact {contact.Id}: {ex.Message}");
                    }
                }

                tracingService.Trace($"AccountAddressSyncPlugin: Completed. Updated: {updatedCount}, Errors: {errorCount}");
            }
            catch (Exception ex)
            {
                tracingService.Trace($"AccountAddressSyncPlugin Error: {ex.Message}");
                tracingService.Trace($"Stack Trace: {ex.StackTrace}");

                throw new InvalidPluginExecutionException(
                    $"An error occurred in AccountAddressSyncPlugin: {ex.Message}", ex);
            }
        }

        private List<Entity> GetRelatedContacts(IOrganizationService service, Guid accountId, ITracingService tracingService)
        {
            tracingService.Trace($"Querying contacts for Account: {accountId}");

            QueryExpression query = new QueryExpression(CONTACT_ENTITY)
            {
                ColumnSet = new ColumnSet(CUSTOM_ADDRESS_FIELD),
                Criteria = new FilterExpression()
                {
                    Conditions =
                    {
                        new ConditionExpression(
                            CONTACT_PARENT_FIELD,
                            ConditionOperator.Equal,
                            accountId)
                    }
                }
            };

            List<Entity> allContacts = new List<Entity>();
            query.PageInfo = new PagingInfo()
            {
                PageNumber = 1,
                Count = 5000
            };

            EntityCollection results;
            do
            {
                results = service.RetrieveMultiple(query);
                allContacts.AddRange(results.Entities);

                query.PageInfo.PageNumber++;
                query.PageInfo.PagingCookie = results.PagingCookie;

            } while (results.MoreRecords);

            return allContacts;
        }

        private void UpdateContactAddress(IOrganizationService service, Guid contactId, string newAddress, ITracingService tracingService)
        {
            tracingService.Trace($"Updating contact: {contactId}");

            Entity contactUpdate = new Entity(CONTACT_ENTITY, contactId);
            contactUpdate[CUSTOM_ADDRESS_FIELD] = newAddress;

            service.Update(contactUpdate);
        }
    }
}