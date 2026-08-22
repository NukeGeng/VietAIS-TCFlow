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

public sealed record RegisterGitHubInstallationRequest(
    long AccountId,
    string AccountLogin,
    GitHubAccountKind AccountKind,
    GitHubRepositorySelectionKind RepositorySelection);

public sealed record ConnectGitHubRepositoryRequest(
    long InstallationId,
    long GitHubRepositoryId,
    string FullName,
    string DefaultBranch);

public sealed class GitHubIntegrationEndpoints : CarterModule
{
    private const int MaximumWebhookPayloadBytes = 1024 * 1024;

    public override void AddRoutes(IEndpointRouteBuilder app)
    {
        var github = app.MapGroup("projects/{projectId:guid}/github")
            .WithTags("github-integration")
            .RequireAuthorization();

        github.MapPut("installations/{installationId:long}", RegisterInstallation)
            .WithName(nameof(RegisterInstallation))
            .Produces<GitHubAppInstallation>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .MapToApiVersion(new ApiVersion(1, 0));

        github.MapPost("repositories", ConnectRepository)
            .WithName(nameof(ConnectRepository))
            .Produces<ConnectedGitHubRepository>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .MapToApiVersion(new ApiVersion(1, 0));

        github.MapPost("repositories/{repositoryId:guid}/initial-scan", TriggerInitialScan)
            .WithName(nameof(TriggerInitialScan))
            .Produces<RepositoryAnalysisRequest>(StatusCodes.Status202Accepted)
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

    private static async Task<IResult> RegisterInstallation(
        Guid projectId,
        long installationId,
        RegisterGitHubInstallationRequest request,
        HttpContext httpContext,
        ISender mediator,
        CancellationToken cancellationToken) =>
        Results.Ok(await mediator.Send(
            new RegisterGitHubInstallationCommand(
                GetActorId(httpContext),
                projectId,
                installationId,
                request.AccountId,
                request.AccountLogin,
                request.AccountKind,
                request.RepositorySelection),
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
                request.GitHubRepositoryId,
                request.FullName,
                request.DefaultBranch),
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
            $"projects/{projectId}/github/repositories/{repositoryId}/initial-scan/{request.Id}",
            request);
    }

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

        var receipt = await mediator.Send(
            new IngestGitHubWebhookCommand(
                request.Headers["X-GitHub-Delivery"].FirstOrDefault() ?? string.Empty,
                request.Headers["X-GitHub-Event"].FirstOrDefault() ?? string.Empty,
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
