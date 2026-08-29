using System.Security.Claims;
using Asp.Versioning;
using Carter;
using FSH.Framework.Core.Exceptions;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using VietAIS.TCFlow.WebApi.RepositoryIntelligence.Management;

namespace VietAIS.TCFlow.WebApi.RepositoryIntelligence.GitHub;

public sealed record ConnectGitHubRepositoryRequest(
    long InstallationId,
    long GitHubRepositoryId);

public sealed record PrepareGitHubAuthorizationRequest(string State, long InstallationId);

public sealed record CompleteGitHubConnectionRequest(
    string State,
    string Code,
    string CodeVerifier);

public sealed class GitHubIntegrationEndpoints : CarterModule
{
    private const int MaximumWebhookPayloadBytes = 1024 * 1024;

    public override void AddRoutes(IEndpointRouteBuilder app)
    {
        var github = app.MapGroup("projects/{projectId:guid}/github")
            .WithTags("github-integration")
            .RequireAuthorization();

        github.MapPost("connections", StartConnection)
            .WithName(nameof(StartConnection))
            .Produces<GitHubInstallationStart>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable)
            .MapToApiVersion(new ApiVersion(1, 0));

        github.MapGet("installations", GetInstallations)
            .WithName(nameof(GetInstallations))
            .Produces<IReadOnlyList<GitHubAppInstallation>>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .MapToApiVersion(new ApiVersion(1, 0));

        github.MapGet("installations/{installationId:long}/repositories", GetRepositories)
            .WithName(nameof(GetRepositories))
            .Produces<IReadOnlyList<GitHubRepositorySummary>>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status502BadGateway)
            .MapToApiVersion(new ApiVersion(1, 0));

        github.MapPost("repositories", ConnectRepository)
            .WithName(nameof(ConnectRepository))
            .Produces<ConnectedGitHubRepository>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .MapToApiVersion(new ApiVersion(1, 0));

        var connections = app.MapGroup("github/connections")
            .WithTags("github-integration")
            .RequireAuthorization();

        connections.MapPost("authorize", PrepareAuthorization)
            .WithName(nameof(PrepareAuthorization))
            .Produces<GitHubAuthorizationStart>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status502BadGateway)
            .MapToApiVersion(new ApiVersion(1, 0));

        connections.MapPost("complete", CompleteConnection)
            .WithName(nameof(CompleteConnection))
            .Produces<GitHubConnectionResult>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status502BadGateway)
            .MapToApiVersion(new ApiVersion(1, 0));

        github.MapPost("repositories/{repositoryId:guid}/initial-scan", TriggerInitialScan)
            .WithName(nameof(TriggerInitialScan))
            .Produces<RepositoryAnalysisRequest>(StatusCodes.Status202Accepted)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .MapToApiVersion(new ApiVersion(1, 0));

        github.MapGet("repositories/{repositoryId:guid}/analyses/latest", GetLatestAnalysis)
            .WithName(nameof(GetLatestAnalysis))
            .Produces<RepositoryAnalysisDetails>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .MapToApiVersion(new ApiVersion(1, 0));

        github.MapGet(
                "repositories/{repositoryId:guid}/analyses/{analysisRequestId:guid}",
                GetAnalysis)
            .WithName(nameof(GetAnalysis))
            .Produces<RepositoryAnalysisDetails>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .MapToApiVersion(new ApiVersion(1, 0));

        app.MapPost("github/webhooks", ReceiveWebhook)
            .WithName(nameof(ReceiveWebhook))
            .WithTags("github-integration")
            .AllowAnonymous()
            .Produces<GitHubWebhookReceipt>(StatusCodes.Status202Accepted)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .MapToApiVersion(new ApiVersion(1, 0));
    }

    private static async Task<IResult> StartConnection(
        Guid projectId,
        HttpContext httpContext,
        ISender mediator,
        CancellationToken cancellationToken) =>
        Results.Ok(await mediator.Send(
            new StartGitHubConnectionCommand(GetActorId(httpContext), projectId),
            cancellationToken));

    private static async Task<IResult> PrepareAuthorization(
        PrepareGitHubAuthorizationRequest request,
        HttpContext httpContext,
        ISender mediator,
        CancellationToken cancellationToken) =>
        Results.Ok(await mediator.Send(
            new PrepareGitHubAuthorizationCommand(
                GetActorId(httpContext),
                request.State,
                request.InstallationId),
            cancellationToken));

    private static async Task<IResult> CompleteConnection(
        CompleteGitHubConnectionRequest request,
        HttpContext httpContext,
        ISender mediator,
        CancellationToken cancellationToken) =>
        Results.Ok(await mediator.Send(
            new CompleteGitHubConnectionCommand(
                GetActorId(httpContext),
                request.State,
                request.Code,
                request.CodeVerifier),
            cancellationToken));

    private static async Task<IResult> GetInstallations(
        Guid projectId,
        HttpContext httpContext,
        ISender mediator,
        CancellationToken cancellationToken) =>
        Results.Ok(await mediator.Send(
            new GetGitHubInstallationsQuery(GetActorId(httpContext), projectId),
            cancellationToken));

    private static async Task<IResult> GetRepositories(
        Guid projectId,
        long installationId,
        HttpContext httpContext,
        ISender mediator,
        CancellationToken cancellationToken) =>
        Results.Ok(await mediator.Send(
            new GetGitHubRepositoriesQuery(
                GetActorId(httpContext),
                projectId,
                installationId),
            cancellationToken));

    private static async Task<IResult> ConnectRepository(
        Guid projectId,
        ConnectGitHubRepositoryRequest request,
        HttpContext httpContext,
        ISender mediator,
        CancellationToken cancellationToken)
    {
        var connected = await mediator.Send(
            new ConnectGitHubRepositoryCommand(
                GetActorId(httpContext),
                projectId,
                request.InstallationId,
                request.GitHubRepositoryId),
            cancellationToken);
        return Results.Created(
            $"projects/{projectId}/repositories/{connected.Repository.Id}",
            connected);
    }

    private static async Task<IResult> TriggerInitialScan(
        Guid projectId,
        Guid repositoryId,
        HttpContext httpContext,
        ISender mediator,
        CancellationToken cancellationToken)
    {
        var request = await mediator.Send(
            new TriggerInitialRepositoryScanCommand(
                GetActorId(httpContext),
                projectId,
                repositoryId),
            cancellationToken);
        return Results.Accepted(
            $"projects/{projectId}/github/repositories/{repositoryId}/analyses/{request.Id}",
            request);
    }

    private static async Task<IResult> GetAnalysis(
        Guid projectId,
        Guid repositoryId,
        Guid analysisRequestId,
        HttpContext httpContext,
        ISender mediator,
        CancellationToken cancellationToken) =>
        Results.Ok(await mediator.Send(
            new GetRepositoryAnalysisQuery(
                GetActorId(httpContext),
                projectId,
                repositoryId,
                analysisRequestId),
            cancellationToken));

    private static async Task<IResult> GetLatestAnalysis(
        Guid projectId,
        Guid repositoryId,
        HttpContext httpContext,
        ISender mediator,
        CancellationToken cancellationToken) =>
        Results.Ok(await mediator.Send(
            new GetLatestRepositoryAnalysisQuery(
                GetActorId(httpContext),
                projectId,
                repositoryId),
            cancellationToken));

    private static async Task<IResult> ReceiveWebhook(
        HttpRequest request,
        IGitHubWebhookSignatureValidator signatureValidator,
        ISender mediator,
        CancellationToken cancellationToken)
    {
        var payload = await ReadPayloadAsync(request, cancellationToken);
        if (!signatureValidator.IsValid(
                payload,
                request.Headers["X-Hub-Signature-256"].FirstOrDefault()))
        {
            return Results.Unauthorized();
        }

        // GitHub sends a signed ping immediately after a webhook is created or
        // updated. It is a handshake, not a source change, so acknowledge it
        // without creating a delivery or analysis request.
        var eventName = request.Headers["X-GitHub-Event"].FirstOrDefault() ?? string.Empty;
        if (string.Equals(eventName.Trim(), "ping", StringComparison.OrdinalIgnoreCase))
        {
            return Results.Accepted(value: new GitHubWebhookReceipt(
                Accepted: true,
                Duplicate: false,
                "ping-acknowledged",
                null));
        }

        var receipt = await mediator.Send(
            new IngestGitHubWebhookCommand(
                request.Headers["X-GitHub-Delivery"].FirstOrDefault() ?? string.Empty,
                eventName,
                payload),
            cancellationToken);
        return Results.Accepted(value: receipt);
    }

    private static async Task<byte[]> ReadPayloadAsync(
        HttpRequest request,
        CancellationToken cancellationToken)
    {
        if (request.ContentLength > MaximumWebhookPayloadBytes)
        {
            throw new ProjectManagementValidationException(
                $"GitHub webhook payload cannot exceed {MaximumWebhookPayloadBytes} bytes.");
        }

        await using var buffer = new MemoryStream();
        var block = new byte[8192];
        while (true)
        {
            var read = await request.Body.ReadAsync(block, cancellationToken);
            if (read == 0)
            {
                break;
            }

            if (buffer.Length + read > MaximumWebhookPayloadBytes)
            {
                throw new ProjectManagementValidationException(
                    $"GitHub webhook payload cannot exceed {MaximumWebhookPayloadBytes} bytes.");
            }

            await buffer.WriteAsync(block.AsMemory(0, read), cancellationToken);
        }

        return buffer.ToArray();
    }

    private static Guid GetActorId(HttpContext httpContext)
    {
        var value = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(value, out var actorId)
            ? actorId
            : throw new UnauthorizedException();
    }
}
