using System.Security.Claims;
using Asp.Versioning;
using Carter;
using FSH.Framework.Core.Exceptions;
using FSH.Framework.Core.Paging;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using VietAIS.TCFlow.WebApi.RepositoryIntelligence.Authorization;

namespace VietAIS.TCFlow.WebApi.RepositoryIntelligence.Management;

public sealed record CreateProjectRequest(string Name);
public sealed record UpdateProjectRequest(string Name);

public sealed record CreateProjectRepositoryRequest(
    string Name,
    RepositoryProviderKind Provider,
    string? LocalPath,
    string? RemoteUrl,
    string DefaultBranch);

public sealed record UpdateProjectRepositoryRequest(
    string Name,
    string? LocalPath,
    string? RemoteUrl,
    string DefaultBranch,
    RepositoryLifecycleStatus Status);

public sealed record CreateProjectComponentRequest(
    Guid RepositoryId,
    string Name,
    ComponentScopeKind Scope,
    string? RootPath);

public sealed record UpdateProjectComponentRequest(
    string Name,
    ComponentScopeKind Scope,
    string? RootPath);

public sealed record CreateProjectFeatureRequest(string Name, string? Description);
public sealed record UpdateProjectFeatureRequest(string Name, string? Description);

public sealed record CreateEngineeringTaskRequest(
    Guid? RepositoryId,
    Guid? ComponentId,
    Guid? FeatureId,
    string Title,
    string? Description,
    TaskPriority Priority,
    Guid? SourceChangeId,
    Guid[] ArtifactIds,
    Guid[] ImpactIds,
    string[] AffectedArtifacts,
    string[] Inputs,
    string[] Outputs,
    string[] BusinessRules,
    Guid[] Dependencies);

public sealed record TransitionEngineeringTaskRequest(TaskLifecycleStatus Status, string? Reason);

public sealed record AssignEngineeringTaskRequest(Guid AssigneeId);

public sealed record ReviewEngineeringTaskRequest(TaskReviewDecision Decision, string? Comment);

public sealed record AddTaskEvidenceRequest(
    TaskEvidenceKind Kind,
    string Summary,
    string? Location,
    Guid? SourceChangeId,
    Guid? ArtifactId,
    Guid? ImpactId,
    decimal? Confidence);

public sealed class ProjectManagementEndpoints : CarterModule
{
    public override void AddRoutes(IEndpointRouteBuilder app)
    {
        var projects = app.MapGroup("projects")
            .WithTags("project-management")
            .RequireAuthorization();

        projects.MapPost(string.Empty, CreateProject)
            .WithName(nameof(CreateProject))
            .Produces<CreateProjectResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .MapToApiVersion(new ApiVersion(1, 0));

        projects.MapGet(string.Empty, SearchProjects)
            .WithName(nameof(SearchProjects))
            .Produces<PagedList<Project>>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .MapToApiVersion(new ApiVersion(1, 0));

        projects.MapGet("{projectId:guid}", GetProject)
            .WithName(nameof(GetProject))
            .Produces<Project>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .MapToApiVersion(new ApiVersion(1, 0));

        projects.MapPut("{projectId:guid}", UpdateProject)
            .WithName(nameof(UpdateProject))
            .Produces<Project>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .MapToApiVersion(new ApiVersion(1, 0));

        projects.MapPost("{projectId:guid}/repositories", CreateRepository)
            .WithName(nameof(CreateRepository))
            .Produces<ProjectRepository>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .MapToApiVersion(new ApiVersion(1, 0));

        projects.MapGet("{projectId:guid}/repositories", SearchRepositories)
            .WithName(nameof(SearchRepositories))
            .Produces<PagedList<ProjectRepository>>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .MapToApiVersion(new ApiVersion(1, 0));

        projects.MapPut("{projectId:guid}/repositories/{repositoryId:guid}", UpdateRepository)
            .WithName(nameof(UpdateRepository))
            .Produces<ProjectRepository>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .MapToApiVersion(new ApiVersion(1, 0));

        projects.MapDelete("{projectId:guid}/repositories/{repositoryId:guid}", DisableRepository)
            .WithName(nameof(DisableRepository))
            .Produces<ProjectRepository>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .MapToApiVersion(new ApiVersion(1, 0));

        projects.MapPost("{projectId:guid}/components", CreateComponent)
            .WithName(nameof(CreateComponent))
            .Produces<ProjectComponent>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .MapToApiVersion(new ApiVersion(1, 0));

        projects.MapPut("{projectId:guid}/components/{componentId:guid}", UpdateComponent)
            .WithName(nameof(UpdateComponent))
            .Produces<ProjectComponent>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .MapToApiVersion(new ApiVersion(1, 0));

        projects.MapDelete("{projectId:guid}/components/{componentId:guid}", DeleteComponent)
            .WithName(nameof(DeleteComponent))
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .MapToApiVersion(new ApiVersion(1, 0));

        projects.MapGet("{projectId:guid}/components", SearchComponents)
            .WithName(nameof(SearchComponents))
            .Produces<PagedList<ProjectComponent>>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .MapToApiVersion(new ApiVersion(1, 0));

        projects.MapPost("{projectId:guid}/features", CreateFeature)
            .WithName(nameof(CreateFeature))
            .Produces<ProjectFeature>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .MapToApiVersion(new ApiVersion(1, 0));

        projects.MapPut("{projectId:guid}/features/{featureId:guid}", UpdateFeature)
            .WithName(nameof(UpdateFeature))
            .Produces<ProjectFeature>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .MapToApiVersion(new ApiVersion(1, 0));

        projects.MapDelete("{projectId:guid}/features/{featureId:guid}", DeleteFeature)
            .WithName(nameof(DeleteFeature))
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .MapToApiVersion(new ApiVersion(1, 0));

        projects.MapGet("{projectId:guid}/features", SearchFeatures)
            .WithName(nameof(SearchFeatures))
            .Produces<PagedList<ProjectFeature>>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .MapToApiVersion(new ApiVersion(1, 0));

        projects.MapPost("{projectId:guid}/tasks", CreateTask)
            .WithName(nameof(CreateTask))
            .Produces<EngineeringTask>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .MapToApiVersion(new ApiVersion(1, 0));

        projects.MapGet("{projectId:guid}/tasks", SearchTasks)
            .WithName(nameof(SearchTasks))
            .Produces<PagedList<EngineeringTask>>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .MapToApiVersion(new ApiVersion(1, 0));

        projects.MapGet("{projectId:guid}/tasks/{taskId:guid}", GetTask)
            .WithName(nameof(GetTask))
            .Produces<EngineeringTaskDetails>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .MapToApiVersion(new ApiVersion(1, 0));

        projects.MapPut("{projectId:guid}/tasks/{taskId:guid}/status", TransitionTask)
            .WithName(nameof(TransitionTask))
            .Produces<EngineeringTask>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .MapToApiVersion(new ApiVersion(1, 0));

        projects.MapPut("{projectId:guid}/tasks/{taskId:guid}/assignment", AssignTask)
            .WithName(nameof(AssignTask))
            .Produces<TaskAssignment>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .MapToApiVersion(new ApiVersion(1, 0));

        projects.MapPost("{projectId:guid}/tasks/{taskId:guid}/reviews", ReviewTask)
            .WithName(nameof(ReviewTask))
            .Produces<TaskReview>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .MapToApiVersion(new ApiVersion(1, 0));

        projects.MapPost("{projectId:guid}/tasks/{taskId:guid}/evidence", AddEvidence)
            .WithName(nameof(AddEvidence))
            .Produces<TaskEvidence>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .MapToApiVersion(new ApiVersion(1, 0));

        projects.MapGet("{projectId:guid}/tasks/{taskId:guid}/history", GetTaskHistory)
            .WithName(nameof(GetTaskHistory))
            .Produces<IReadOnlyList<TaskVersion>>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .MapToApiVersion(new ApiVersion(1, 0));
    }

    private static async Task<IResult> CreateProject(
        CreateProjectRequest request,
        HttpContext httpContext,
        ISender mediator,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(
            new CreateProjectCommand(GetActorId(httpContext), request.Name),
            cancellationToken);
        return Results.Created($"projects/{result.Project.Id}", result);
    }

    private static async Task<IResult> SearchProjects(
        int pageNumber,
        int pageSize,
        string? keyword,
        HttpContext httpContext,
        ISender mediator,
        CancellationToken cancellationToken) =>
        Results.Ok(await mediator.Send(
            new SearchProjectsQuery(GetActorId(httpContext), pageNumber, pageSize, keyword),
            cancellationToken));

    private static async Task<IResult> GetProject(
        Guid projectId,
        HttpContext httpContext,
        ISender mediator,
        CancellationToken cancellationToken) =>
        Results.Ok(await mediator.Send(
            new GetProjectQuery(GetActorId(httpContext), projectId),
            cancellationToken));

    private static async Task<IResult> UpdateProject(
        Guid projectId,
        UpdateProjectRequest request,
        HttpContext httpContext,
        ISender mediator,
        CancellationToken cancellationToken) =>
        Results.Ok(await mediator.Send(
            new UpdateProjectCommand(GetActorId(httpContext), projectId, request.Name),
            cancellationToken));

    private static async Task<IResult> CreateRepository(
        Guid projectId,
        CreateProjectRepositoryRequest request,
        HttpContext httpContext,
        ISender mediator,
        CancellationToken cancellationToken)
    {
        var repository = await mediator.Send(
            new CreateProjectRepositoryCommand(
                GetActorId(httpContext),
                projectId,
                request.Name,
                request.Provider,
                request.LocalPath,
                request.RemoteUrl,
                request.DefaultBranch),
            cancellationToken);
        return Results.Created($"projects/{projectId}/repositories/{repository.Id}", repository);
    }

    private static async Task<IResult> SearchRepositories(
        Guid projectId,
        int pageNumber,
        int pageSize,
        string? keyword,
        RepositoryLifecycleStatus? status,
        HttpContext httpContext,
        ISender mediator,
        CancellationToken cancellationToken) =>
        Results.Ok(await mediator.Send(
            new SearchProjectRepositoriesQuery(
                GetActorId(httpContext),
                projectId,
                pageNumber,
                pageSize,
                keyword,
                status),
            cancellationToken));

    private static async Task<IResult> UpdateRepository(
        Guid projectId,
        Guid repositoryId,
        UpdateProjectRepositoryRequest request,
        HttpContext httpContext,
        ISender mediator,
        CancellationToken cancellationToken) =>
        Results.Ok(await mediator.Send(
            new UpdateProjectRepositoryCommand(
                GetActorId(httpContext),
                projectId,
                repositoryId,
                request.Name,
                request.LocalPath,
                request.RemoteUrl,
                request.DefaultBranch,
                request.Status),
            cancellationToken));

    private static async Task<IResult> DisableRepository(
        Guid projectId,
        Guid repositoryId,
        HttpContext httpContext,
        ISender mediator,
        CancellationToken cancellationToken) =>
        Results.Ok(await mediator.Send(
            new DisableProjectRepositoryCommand(
                GetActorId(httpContext),
                projectId,
                repositoryId),
            cancellationToken));

    private static async Task<IResult> CreateComponent(
        Guid projectId,
        CreateProjectComponentRequest request,
        HttpContext httpContext,
        ISender mediator,
        CancellationToken cancellationToken)
    {
        var component = await mediator.Send(
            new CreateProjectComponentCommand(
                GetActorId(httpContext),
                projectId,
                request.RepositoryId,
                request.Name,
                request.Scope,
                request.RootPath),
            cancellationToken);
        return Results.Created($"projects/{projectId}/components/{component.Id}", component);
    }

    private static async Task<IResult> UpdateComponent(
        Guid projectId,
        Guid componentId,
        UpdateProjectComponentRequest request,
        HttpContext httpContext,
        ISender mediator,
        CancellationToken cancellationToken) =>
        Results.Ok(await mediator.Send(
            new UpdateProjectComponentCommand(
                GetActorId(httpContext),
                projectId,
                componentId,
                request.Name,
                request.Scope,
                request.RootPath),
            cancellationToken));

    private static async Task<IResult> DeleteComponent(
        Guid projectId,
        Guid componentId,
        HttpContext httpContext,
        ISender mediator,
        CancellationToken cancellationToken)
    {
        await mediator.Send(
            new DeleteProjectComponentCommand(GetActorId(httpContext), projectId, componentId),
            cancellationToken);
        return Results.NoContent();
    }

    private static async Task<IResult> SearchComponents(
        Guid projectId,
        int pageNumber,
        int pageSize,
        string? keyword,
        Guid? repositoryId,
        ComponentScopeKind? scope,
        HttpContext httpContext,
        ISender mediator,
        CancellationToken cancellationToken) =>
        Results.Ok(await mediator.Send(
            new SearchProjectComponentsQuery(
                GetActorId(httpContext),
                projectId,
                pageNumber,
                pageSize,
                keyword,
                repositoryId,
                scope),
            cancellationToken));

    private static async Task<IResult> CreateFeature(
        Guid projectId,
        CreateProjectFeatureRequest request,
        HttpContext httpContext,
        ISender mediator,
        CancellationToken cancellationToken)
    {
        var feature = await mediator.Send(
            new CreateProjectFeatureCommand(
                GetActorId(httpContext),
                projectId,
                request.Name,
                request.Description),
            cancellationToken);
        return Results.Created($"projects/{projectId}/features/{feature.Id}", feature);
    }

    private static async Task<IResult> UpdateFeature(
        Guid projectId,
        Guid featureId,
        UpdateProjectFeatureRequest request,
        HttpContext httpContext,
        ISender mediator,
        CancellationToken cancellationToken) =>
        Results.Ok(await mediator.Send(
            new UpdateProjectFeatureCommand(
                GetActorId(httpContext),
                projectId,
                featureId,
                request.Name,
                request.Description),
            cancellationToken));

    private static async Task<IResult> DeleteFeature(
        Guid projectId,
        Guid featureId,
        HttpContext httpContext,
        ISender mediator,
        CancellationToken cancellationToken)
    {
        await mediator.Send(
            new DeleteProjectFeatureCommand(GetActorId(httpContext), projectId, featureId),
            cancellationToken);
        return Results.NoContent();
    }

    private static async Task<IResult> SearchFeatures(
        Guid projectId,
        int pageNumber,
        int pageSize,
        string? keyword,
        HttpContext httpContext,
        ISender mediator,
        CancellationToken cancellationToken) =>
        Results.Ok(await mediator.Send(
            new SearchProjectFeaturesQuery(
                GetActorId(httpContext),
                projectId,
                pageNumber,
                pageSize,
                keyword),
            cancellationToken));

    private static async Task<IResult> CreateTask(
        Guid projectId,
        CreateEngineeringTaskRequest request,
        HttpContext httpContext,
        ISender mediator,
        CancellationToken cancellationToken)
    {
        var task = await mediator.Send(
            new CreateEngineeringTaskCommand(
                GetActorId(httpContext),
                projectId,
                request.RepositoryId,
                request.ComponentId,
                request.FeatureId,
                request.Title,
                request.Description,
                request.Priority,
                request.SourceChangeId,
                request.ArtifactIds,
                request.ImpactIds,
                request.AffectedArtifacts,
                request.Inputs,
                request.Outputs,
                request.BusinessRules,
                request.Dependencies),
            cancellationToken);
        return Results.Created($"projects/{projectId}/tasks/{task.Id}", task);
    }

    private static async Task<IResult> SearchTasks(
        Guid projectId,
        int pageNumber,
        int pageSize,
        string? keyword,
        TaskLifecycleStatus? status,
        TaskPriority? priority,
        Guid? repositoryId,
        Guid? featureId,
        Guid? assigneeId,
        HttpContext httpContext,
        ISender mediator,
        CancellationToken cancellationToken) =>
        Results.Ok(await mediator.Send(
            new SearchEngineeringTasksQuery(
                GetActorId(httpContext),
                projectId,
                pageNumber,
                pageSize,
                keyword,
                status,
                priority,
                repositoryId,
                featureId,
                assigneeId),
            cancellationToken));

    private static async Task<IResult> GetTask(
        Guid projectId,
        Guid taskId,
        HttpContext httpContext,
        ISender mediator,
        CancellationToken cancellationToken) =>
        Results.Ok(await mediator.Send(
            new GetEngineeringTaskQuery(GetActorId(httpContext), projectId, taskId),
            cancellationToken));

    private static async Task<IResult> TransitionTask(
        Guid projectId,
        Guid taskId,
        TransitionEngineeringTaskRequest request,
        HttpContext httpContext,
        ISender mediator,
        CancellationToken cancellationToken) =>
        Results.Ok(await mediator.Send(
            new TransitionEngineeringTaskCommand(
                GetActorId(httpContext),
                projectId,
                taskId,
                request.Status,
                request.Reason),
            cancellationToken));

    private static async Task<IResult> AssignTask(
        Guid projectId,
        Guid taskId,
        AssignEngineeringTaskRequest request,
        HttpContext httpContext,
        ISender mediator,
        CancellationToken cancellationToken) =>
        Results.Ok(await mediator.Send(
            new AssignEngineeringTaskCommand(
                GetActorId(httpContext),
                projectId,
                taskId,
                request.AssigneeId),
            cancellationToken));

    private static async Task<IResult> ReviewTask(
        Guid projectId,
        Guid taskId,
        ReviewEngineeringTaskRequest request,
        HttpContext httpContext,
        ISender mediator,
        CancellationToken cancellationToken)
    {
        var review = await mediator.Send(
            new ReviewEngineeringTaskCommand(
                GetActorId(httpContext),
                projectId,
                taskId,
                request.Decision,
                request.Comment),
            cancellationToken);
        return Results.Created($"projects/{projectId}/tasks/{taskId}/reviews/{review.Id}", review);
    }

    private static async Task<IResult> AddEvidence(
        Guid projectId,
        Guid taskId,
        AddTaskEvidenceRequest request,
        HttpContext httpContext,
        ISender mediator,
        CancellationToken cancellationToken)
    {
        var evidence = await mediator.Send(
            new AddTaskEvidenceCommand(
                GetActorId(httpContext),
                TaskActorType.User,
                projectId,
                taskId,
                request.Kind,
                request.Summary,
                request.Location,
                request.SourceChangeId,
                request.ArtifactId,
                request.ImpactId,
                request.Confidence),
            cancellationToken);
        return Results.Created($"projects/{projectId}/tasks/{taskId}/evidence/{evidence.Id}", evidence);
    }

    private static async Task<IResult> GetTaskHistory(
        Guid projectId,
        Guid taskId,
        HttpContext httpContext,
        ISender mediator,
        CancellationToken cancellationToken) =>
        Results.Ok(await mediator.Send(
            new GetTaskHistoryQuery(GetActorId(httpContext), projectId, taskId),
            cancellationToken));

    private static Guid GetActorId(HttpContext httpContext)
    {
        var value = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(value, out var actorId)
            ? actorId
            : throw new UnauthorizedException();
    }
}
