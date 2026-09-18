using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using Microsoft.AspNetCore.Authorization.Policy;

namespace BizfreeApp.Infrastructure.Security;

public class FineGrainedAuthorizationHandler : IAuthorizationMiddlewareResultHandler
{
    private readonly AuthorizationMiddlewareResultHandler _defaultHandler = new();

    public async Task HandleAsync(
        RequestDelegate next,
        HttpContext context,
        AuthorizationPolicy policy,
        PolicyAuthorizationResult authorizeResult)
    {
        if (authorizeResult.Forbidden && authorizeResult.AuthorizationFailure is not null)
        {
            var missingPermissions = authorizeResult.AuthorizationFailure.FailedRequirements
                .OfType<ClaimsAuthorizationRequirement>()
                .Where(requirement => requirement.ClaimType == "permission")
                .SelectMany(requirement => requirement.AllowedValues ?? Enumerable.Empty<string>())
                .Distinct()
                .ToArray();

            if (missingPermissions.Length > 0)
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;

                await context.Response.WriteAsJsonAsync(new
                {
                    error = "Permission Denied",
                    message = $"Your role is missing the following required permissions: {string.Join(", ", missingPermissions)}",
                    code = "MISSING_PERMISSION",
                    missingPermissions
                });

                return;
            }
        }

        await _defaultHandler.HandleAsync(next, context, policy, authorizeResult);
    }
}
