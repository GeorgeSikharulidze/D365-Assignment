using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;

namespace GS.D365.Plugins
{
    public class RegistrationMaterialsPlugin : IPlugin
    {
        private const string REGISTRATION_ENTITY = "gs_registrations";
        private const string MATERIAL_ENTITY = "gs_materials";
        private const string REG_COURSE_FIELD = "gs_course";
        private const string REG_MATERIALS_NEEDED = "gs_materialsneeded";
        private const string MAT_NAME_FIELD = "gs_materialname";
        private const string MAT_COURSE_FIELD = "gs_course";

        public void Execute(IServiceProvider serviceProvider)
        {
            ITracingService tracingService = (ITracingService)serviceProvider.GetService(typeof(ITracingService));
            IPluginExecutionContext context = (IPluginExecutionContext)serviceProvider.GetService(typeof(IPluginExecutionContext));
            IOrganizationServiceFactory serviceFactory = (IOrganizationServiceFactory)serviceProvider.GetService(typeof(IOrganizationServiceFactory));
            IOrganizationService service = serviceFactory.CreateOrganizationService(context.UserId);

            try
            {
                tracingService.Trace("RegistrationMaterialsPlugin: Execution started");

                if (context.MessageName.ToLower() != "create")
                {
                    tracingService.Trace("Not a Create message. Exiting.");
                    return;
                }

                if(!context.InputParameters.Contains("Target") || !(context.InputParameters["Target"] is Entity))
                {
                    tracingService.Trace("No Target entity found. Exiting.");
                    return;
                }

                Entity registration = (Entity)context.InputParameters["Target"];

                if (registration.LogicalName != REGISTRATION_ENTITY)
                {
                    tracingService.Trace($"Entity is {registration.LogicalName}, not {REGISTRATION_ENTITY}. Exiting.");
                    return;
                }

                tracingService.Trace($"Processing Registration: {registration.Id}");

                if (!registration.Contains(REG_COURSE_FIELD))
                {
                    tracingService.Trace("No Course field in registration. Exiting.");
                    return;
                }

                EntityReference courseReference = registration.GetAttributeValue<EntityReference>(REG_COURSE_FIELD);

                if (courseReference == null)
                {
                    tracingService.Trace("Course reference is null. Exiting.");
                    return;
                }

                Guid courseId = courseReference.Id;
                tracingService.Trace($"Course ID: {courseId}");

                QueryExpression queryExpression = new QueryExpression(MATERIAL_ENTITY)
                {
                    ColumnSet = new ColumnSet(MAT_NAME_FIELD),
                    Criteria = new FilterExpression()
                    {
                        Conditions =
                        {
                            new ConditionExpression(
                                MAT_COURSE_FIELD,
                                ConditionOperator.Equal,
                                courseId)
                        }
                    },
                    Orders =
                    {
                        new OrderExpression(MAT_NAME_FIELD, OrderType.Ascending)
                    }
                };
                tracingService.Trace("Querying materials...");

                EntityCollection materials = service.RetrieveMultiple(queryExpression);

                if (materials.Entities.Count == 0)
                {
                    tracingService.Trace("No materials found for this course.");
                    UpdateMaterialsNeeded(service, registration.Id, "No materials required for this course.", tracingService);
                    return;
                }
                tracingService.Trace($"Found {materials.Entities.Count} materials");


                List<string> materialNames = new List<string>();

                foreach( var material in materials.Entities)
                {
                    string materialName = material.GetAttributeValue<string>(MAT_NAME_FIELD);

                    if (!string.IsNullOrEmpty(materialName)) materialNames.Add(materialName);
                }

                string materialsNeeded = string.Join(",\n", materialNames);
                tracingService.Trace($"Materials Needed: {materialsNeeded}");

                UpdateMaterialsNeeded(service, registration.Id, materialsNeeded, tracingService);
            }
            catch (Exception ex)
            {
                tracingService.Trace($"Error: {ex.Message}");
                tracingService.Trace($"Stack Trace: {ex.StackTrace}");

                throw new InvalidPluginExecutionException(
                    $"An error occurred in RegistrationMaterialsPlugin: {ex.Message}", ex);
            }
        }

        private void UpdateMaterialsNeeded(IOrganizationService service, Guid registrationId, string materialsNeeded, ITracingService tracingService)
        {
            tracingService.Trace($"Updating Registration {registrationId} with materials");

            Entity updateRegistration = new Entity(REGISTRATION_ENTITY, registrationId);
            updateRegistration[REG_MATERIALS_NEEDED] = materialsNeeded;

            service.Update(updateRegistration);

            tracingService.Trace("Update complete");
        }
    }
}
