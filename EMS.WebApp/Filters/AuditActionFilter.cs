using EMS.WebApp.Services;
using Microsoft.AspNetCore.Mvc.Filters;
using System.Diagnostics;

namespace EMS.WebApp.Filters
{
    public class AuditActionFilter : IAsyncActionFilter
    {
        private readonly IAuditService _auditService;

        public AuditActionFilter(IAuditService auditService)
        {
            _auditService = auditService;
        }

        public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            var stopwatch = Stopwatch.StartNew();

            var controller = context.RouteData.Values["controller"]?.ToString();
            var action = context.RouteData.Values["action"]?.ToString();

            ActionExecutedContext? executedContext = null;
            Exception? exception = null;

            try
            {
                executedContext = await next();
            }
            catch (Exception ex)
            {
                exception = ex;
                throw;
            }
            finally
            {
                stopwatch.Stop();

                var result = exception != null ? "Failed" : "Success";
                var errorMessage = exception?.Message;

                // Log significant actions (exclude routine GET requests to Index)
                if (ShouldLogAction(controller, action))
                {
                    await _auditService.LogUserActionAsync(
                        action: $"{action}",
                        controller: controller,
                        description: GetActionDescription(controller, action),
                        parameters: context.ActionArguments.Count > 0 ? context.ActionArguments : null,
                        result: result,
                        errorMessage: errorMessage,
                        executionTime: stopwatch.Elapsed
                    );
                }
            }
        }

        private static bool ShouldLogAction(string? controller, string? action)
        {
            // Skip logging for these routine actions
            if (action?.ToLower() == "index" &&
                (controller?.ToLower() == "home" || controller?.ToLower() == "account"))
                return false;

            // Skip audit controller to prevent self-logging
            if (controller?.ToLower() == "audit")
                return false;

            return true;
        }

        private static string GetActionDescription(string? controller, string? action)
        {
            return (controller?.ToLower(), action?.ToLower()) switch
            {
                ("account", "login") => "User logged in",
                ("account", "logout") => "User logged out",
                (_, "create") or (_, "add") => $"Created new {controller?.ToLower()} record",
                (_, "edit") or (_, "update") => $"Updated {controller?.ToLower()} record",
                (_, "delete") => $"Deleted {controller?.ToLower()} record",
                (_, "approve") => $"Approved {controller?.ToLower()} record",
                (_, "reject") => $"Rejected {controller?.ToLower()} record",
                _ => $"Performed {action} on {controller}"
            };
        }
    }
}