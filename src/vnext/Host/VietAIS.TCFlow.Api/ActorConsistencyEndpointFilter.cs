using FSH.Framework.Shared.Identity.Claims;
using System.Diagnostics.CodeAnalysis;

namespace VietAIS.TCFlow.Api;

/// <summary>
/// Prevents command payloads from impersonating another actor.
/// Authentication is enforced by FullStackHero's fallback policy; this filter
/// binds the actor recorded in the event metadata to the authenticated user.
/// </summary>
[SuppressMessage("Performance", "CA1812", Justification = "The ASP.NET Core endpoint filter factory instantiates this type at runtime.")]
internal sealed class ActorConsistencyEndpointFilter : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next)
    {
        var authenticatedActor = context.HttpContext.User.GetUserId();

        foreach (var argument in context.Arguments)
        {
            if (argument is null)
            {
                continue;
            }

            var actorProperty = argument.GetType().GetProperty("ActorId")
                ?? argument.GetType().GetProperty("OwnerId");

            if (actorProperty?.PropertyType != typeof(string))
            {
                continue;
            }

            var suppliedActor = actorProperty.GetValue(argument) as string;
            if (string.IsNullOrWhiteSpace(suppliedActor))
            {
                return Results.BadRequest(new { error = $"{actorProperty.Name} is required." });
            }

            if (string.IsNullOrWhiteSpace(authenticatedActor)
                || !string.Equals(suppliedActor.Trim(), authenticatedActor.Trim(), StringComparison.Ordinal))
            {
                return Results.Json(
                    new { error = "The command actor must match the authenticated user." },
                    statusCode: StatusCodes.Status403Forbidden);
            }
        }

        return await next(context).ConfigureAwait(false);
    }
}
