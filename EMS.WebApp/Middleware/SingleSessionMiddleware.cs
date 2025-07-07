using EMS.WebApp.Services;
using Microsoft.AspNetCore.Authentication;

namespace EMS.WebApp.Middleware
{
    public class SingleSessionMiddleware
    {
        private readonly RequestDelegate _next;

        public SingleSessionMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context, IAccountLoginRepository repo)
        {
            var path = context.Request.Path.Value?.ToLower();

            // ✅ Skip validation for these safe paths to prevent redirect loops
            if (path != null && (
                path.Contains("/account/login") ||
                path.Contains("/account/logout") ||
                path.Contains("/account/logoutview") ||
                path.Contains("/account/confirmsessionoverride") ||
                path.Contains("/account/proceedconfirmedlogin") ||
                path.Contains("/session/check")
            ))
            {
                await _next(context);
                return;
            }

            if (context.User.Identity?.IsAuthenticated == true)
            {
                var userName = context.User.Identity.Name;
                var sessionTokenClaim = context.User.FindFirst("SessionToken")?.Value;

                if (userName != null && sessionTokenClaim != null)
                {
                    var user = await repo.GetByEmailAsync(userName);
                    Console.WriteLine($"[Middleware] User: {userName}, Claim Token: {sessionTokenClaim}, DB Token: {user?.SessionToken}");

                    if (user?.SessionToken != sessionTokenClaim)
                    {
                        Console.WriteLine("[Middleware] Token mismatch — logging out");
                        await context.SignOutAsync();

                        var isAjax = context.Request.Headers["X-Requested-With"] == "XMLHttpRequest" ||
                                     context.Request.Headers["Accept"].ToString().Contains("application/json");

                        if (isAjax)
                        {
                            context.Response.StatusCode = 401;
                        }
                        else
                        {
                            context.Response.Redirect("/Account/LogoutView?reason=SessionExpired");
                        }

                        return;
                    }
                }
            }

            await _next(context);
        }
    }
}
