using HotelManagement.Application.Common.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace HotelManagement.API.Middleware;

/// <summary>
/// Marks a controller/action as belonging to a feature module. Staff users may
/// only reach it if that module key is in their "perm" claim. Admins bypass.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class ModuleAccessAttribute : Attribute
{
    public string Module { get; }
    public ModuleAccessAttribute(string module) => Module = module.ToLowerInvariant();
}

public sealed class ModuleAccessFilter : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var attr = context.ActionDescriptor.EndpointMetadata
            .OfType<ModuleAccessAttribute>()
            .LastOrDefault();

        if (attr != null)
        {
            var user = context.HttpContext.User;
            var role = user.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value;

            // Only Staff are gated; admins (and unauthenticated → handled elsewhere) pass.
            if (string.Equals(role, "Staff", StringComparison.OrdinalIgnoreCase))
            {
                var perms = (user.FindFirst("perm")?.Value ?? "")
                    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

                if (!perms.Contains(attr.Module, StringComparer.OrdinalIgnoreCase))
                {
                    context.Result = new ObjectResult(
                        ApiResponse<object>.Fail($"You don't have access to {attr.Module}."))
                    { StatusCode = StatusCodes.Status403Forbidden };
                    return;
                }
            }
        }

        await next();
    }
}
