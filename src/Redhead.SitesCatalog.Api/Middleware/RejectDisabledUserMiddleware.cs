using Microsoft.AspNetCore.Identity;
using Redhead.SitesCatalog.Domain.Entities;

namespace Redhead.SitesCatalog.Api.Middleware;

/// <summary>
/// Rejects requests from disabled users (IsActive=false). Runs after authentication.
/// Signs out the user and returns 401 so existing sessions are invalidated on next request.
/// </summary>
public class RejectDisabledUserMiddleware
{
    private readonly RequestDelegate _next;

    public RejectDisabledUserMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, UserManager<ApplicationUser> userManager, SignInManager<ApplicationUser> signInManager)
    {
        if (context.User.Identity?.IsAuthenticated == true)
        {
            var user = await userManager.GetUserAsync(context.User);
            if (user != null && !user.IsActive)
            {
                await signInManager.SignOutAsync();
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                context.Response.ContentType = "application/json";
                await context.Response.WriteAsJsonAsync(new { message = "Your account has been disabled." });
                return;
            }
        }

        await _next(context);
    }
}
