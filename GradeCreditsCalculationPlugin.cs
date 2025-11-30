using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System;

namespace GS.D365.Plugins
{
    public class GradeCreditsCalculationPlugin : IPlugin
    {
        private const string REGISTRATION_ENTITY = "gs_registrations";
        private const string COURSE_ENTITY = "gs_courses";
        private const string STUDENT_ENTITY = "gs_student";
        private const string REG_GRADE_FIELD = "gs_grade";
        private const string REG_PASSED_FIELD = "gs_passed";
        private const string REG_CREDITS_EARNED_FIELD = "gs_creditsearned";
        private const string REG_STUDENT_FIELD = "gs_student";
        private const string REG_COURSE_FIELD = "gs_course";
        private const string COURSE_CREDITS_FIELD = "gs_credits";
        private const string STUDENT_TOTAL_CREDITS_FIELD = "gs_totalcredits";
        private const string STUDENT_GPA_FIELD = "gs_gpa";

        private const int PASSING_GRADE = 51;


        public void Execute(IServiceProvider serviceProvider)
        {
            ITracingService tracingService = (ITracingService)serviceProvider.GetService(typeof(ITracingService));
            IPluginExecutionContext context = (IPluginExecutionContext)serviceProvider.GetService(typeof(IPluginExecutionContext));
            IOrganizationServiceFactory serviceFactory = (IOrganizationServiceFactory)serviceProvider.GetService(typeof(IOrganizationServiceFactory));
            IOrganizationService service = serviceFactory.CreateOrganizationService(context.UserId);

            try
            {
                tracingService.Trace("GradeCreditsCalculationPlugin: Execution started");

                string messageName = context.MessageName.ToLower();
                if (messageName != "create" && messageName != "update")
                {
                    tracingService.Trace("Not a Create or Update message. Exiting.");
                    return;
                }

                if (!context.InputParameters.Contains("Target") || !(context.InputParameters["Target"] is Entity))
                {
                    tracingService.Trace("No Target entity found. Exiting.");
                    return;
                }

                Entity targetRegistration = (Entity)context.InputParameters["Target"];

                if (targetRegistration.LogicalName != REGISTRATION_ENTITY)
                {
                    tracingService.Trace($"Entity is {targetRegistration.LogicalName}, not {REGISTRATION_ENTITY}. Exiting.");
                    return;
                }

                if (!targetRegistration.Contains(REG_GRADE_FIELD))
                {
                    tracingService.Trace("Grade field not in update payload. Exiting.");
                    return;
                }

                tracingService.Trace($"Processing Registration: {targetRegistration.Id}");

                Entity fullRegistration = service.Retrieve(
                    REGISTRATION_ENTITY,
                    targetRegistration.Id,
                    new ColumnSet(REG_STUDENT_FIELD, REG_COURSE_FIELD, REG_GRADE_FIELD)
                );

                tracingService.Trace("Retrieved full registration record");


                int? grade = targetRegistration.Contains(REG_GRADE_FIELD)
                    ? targetRegistration.GetAttributeValue<int?>(REG_GRADE_FIELD)
                    : fullRegistration.GetAttributeValue<int?>(REG_GRADE_FIELD);

                if (!grade.HasValue)
                {
                    tracingService.Trace("Grade is null. Setting Passed = No, Credits = 0");
                    UpdateRegistrationResults(service, targetRegistration.Id, false, 0, tracingService);

                    EntityReference studentRef = fullRegistration.GetAttributeValue<EntityReference>(REG_STUDENT_FIELD);
                    if (studentRef != null)
                    {
                        RecalculateStudentStats(service, studentRef.Id, tracingService);
                    }
                    return;
                }

                tracingService.Trace($"Grade value: {grade.Value}");

                bool passed = grade.Value >= PASSING_GRADE;
                tracingService.Trace($"Passed: {passed} (Grade {grade.Value} >= {PASSING_GRADE}? {passed})");

                int creditsEarned = 0;

                if (passed)
                {
                    EntityReference courseRef = fullRegistration.GetAttributeValue<EntityReference>(REG_COURSE_FIELD);

                    if (courseRef != null)
                    {
                        tracingService.Trace($"Retrieving Course: {courseRef.Id}");

                        Entity course = service.Retrieve(
                            COURSE_ENTITY,
                            courseRef.Id,
                            new ColumnSet(COURSE_CREDITS_FIELD)
                        );

                        creditsEarned = course.GetAttributeValue<int?>(COURSE_CREDITS_FIELD) ?? 0;
                        tracingService.Trace($"Course Credits: {creditsEarned}");
                    }
                    else
                    {
                        tracingService.Trace("No Course linked to registration. Credits = 0");
                    }
                }
                else
                {
                    tracingService.Trace("Student did not pass. Credits Earned = 0");
                }

                UpdateRegistrationResults(service, targetRegistration.Id, passed, creditsEarned, tracingService);

                EntityReference studentReference = fullRegistration.GetAttributeValue<EntityReference>(REG_STUDENT_FIELD);

                if (studentReference != null)
                {
                    tracingService.Trace($"Updating Student Total Credits: {studentReference.Id}");
                    RecalculateStudentStats(service, studentReference.Id, tracingService);
                }
                else
                {
                    tracingService.Trace("No Student linked to registration. Skipping total credits update.");
                }

                tracingService.Trace("GradeCreditsCalculationPlugin: Execution completed successfully");
            }
            catch (Exception ex)
            {
                tracingService.Trace($"Error: {ex.Message}");
                tracingService.Trace($"Stack Trace: {ex.StackTrace}");

                throw new InvalidPluginExecutionException(
                    $"An error occurred in GradeCreditsCalculationPlugin: {ex.Message}", ex);
            }
        }

        private void UpdateRegistrationResults(IOrganizationService service, Guid registrationId, bool passed, int creditsEarned, ITracingService tracingService)
        {
            tracingService.Trace($"Updating Registration - Passed: {passed}, Credits Earned: {creditsEarned}");

            Entity updateRegistration = new Entity(REGISTRATION_ENTITY, registrationId);
            updateRegistration[REG_PASSED_FIELD] = passed;
            updateRegistration[REG_CREDITS_EARNED_FIELD] = creditsEarned;

            service.Update(updateRegistration);

            tracingService.Trace("Registration updated successfully");
        }

        private void RecalculateStudentStats(IOrganizationService service, Guid studentId, ITracingService tracingService)
        {
            tracingService.Trace($"Recalculating stats for Student: {studentId}");

            QueryExpression query = new QueryExpression(REGISTRATION_ENTITY)
            {
                ColumnSet = new ColumnSet(REG_CREDITS_EARNED_FIELD, REG_GRADE_FIELD, REG_PASSED_FIELD, REG_COURSE_FIELD),
                Criteria = new FilterExpression()
                {
                    Conditions =
                    {
                        new ConditionExpression(REG_STUDENT_FIELD, ConditionOperator.Equal, studentId)
                    }
                }
            };

            EntityCollection allRegistrations = service.RetrieveMultiple(query);
            tracingService.Trace($"Found {allRegistrations.Entities.Count} total registrations");

            int totalCreditsEarned = 0;
            decimal totalGpaPoints = 0;
            int totalCreditsAttempted = 0;

            foreach (Entity registration in allRegistrations.Entities)
            {
                int? grade = registration.GetAttributeValue<int?>(REG_GRADE_FIELD);
                bool? passed = registration.GetAttributeValue<bool?>(REG_PASSED_FIELD);
                int creditsEarned = registration.GetAttributeValue<int?>(REG_CREDITS_EARNED_FIELD) ?? 0;

                if (passed == true)
                {
                    totalCreditsEarned += creditsEarned;
                }

                if (grade.HasValue)
                {
                    EntityReference courseRef = registration.GetAttributeValue<EntityReference>(REG_COURSE_FIELD);
                    if (courseRef != null)
                    {
                        Entity course = service.Retrieve(
                            COURSE_ENTITY,
                            courseRef.Id,
                            new ColumnSet(COURSE_CREDITS_FIELD)
                        );

                        int courseCredits = course.GetAttributeValue<int?>(COURSE_CREDITS_FIELD) ?? 0;
                        decimal gpaPoints = ConvertGradeToGpaPoints(grade.Value);

                        totalGpaPoints += gpaPoints * courseCredits;
                        totalCreditsAttempted += courseCredits;

                        tracingService.Trace($"Course: Grade={grade.Value}, GPA Points={gpaPoints}, Credits={courseCredits}");
                    }
                }
            }

            decimal gpa = 0;
            if (totalCreditsAttempted > 0)
            {
                gpa = Math.Round(totalGpaPoints / totalCreditsAttempted, 2);
            }

            Entity updateStudent = new Entity(STUDENT_ENTITY, studentId);
            updateStudent[STUDENT_TOTAL_CREDITS_FIELD] = totalCreditsEarned;
            updateStudent[STUDENT_GPA_FIELD] = gpa;

            service.Update(updateStudent);

            tracingService.Trace("Student stats updated successfully");
        }

        private decimal ConvertGradeToGpaPoints(int grade)
        {
            if (grade >= 90) return 4.0m;
            if (grade >= 80) return 3.0m;
            if (grade >= 70) return 2.0m;
            if (grade >= 60) return 1.0m;
            if (grade >= 51) return 0.5m;
            return 0.0m;
        }

    }
}
