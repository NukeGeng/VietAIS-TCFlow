var builder = DistributedApplication.CreateBuilder(args);

var database = builder.AddPostgres("postgres")
    .WithDataVolume()
    .AddDatabase("database", "vietais_tcflow");

var redis = builder.AddRedis("redis")
    .WithDataVolume();

var jwtKey = builder.AddParameter("jwt-key", secret: true);
var hangfirePassword = builder.AddParameter("hangfire-password", secret: true);
var bootstrapAdminPassword = builder.AddParameter("bootstrap-admin-password", secret: true);
var githubWebhookSecret = builder.AddParameter("github-webhook-secret", secret: true);
var githubAppId = builder.AddParameter("github-app-id");
var githubAppSlug = builder.AddParameter("github-app-slug");
var githubClientId = builder.AddParameter("github-client-id");
var githubClientSecret = builder.AddParameter("github-client-secret", secret: true);
var githubPrivateKeyBase64 = builder.AddParameter("github-private-key-base64", secret: true);
var repositoryReasoningEnabled = builder.Configuration["RepositoryReasoning:Enabled"] ?? "false";
var repositoryReasoningExecutable = builder.Configuration["RepositoryReasoning:ExecutablePath"] ?? "codex";
var repositoryReasoningWorkingDirectory =
    builder.Configuration["RepositoryReasoning:WorkingDirectory"] ?? ".tcflow/codex-reasoning";
var repositoryReasoningModel = builder.Configuration["RepositoryReasoning:Model"];

var webApi = builder.AddProject<Projects.Server>("webapi")
    .WithReference(database)
    .WithReference(redis)
    .WithEnvironment("JwtOptions__Key", jwtKey)
    .WithEnvironment("HangfireOptions__Password", hangfirePassword)
    .WithEnvironment("BootstrapAdminOptions__Password", bootstrapAdminPassword)
    .WithEnvironment("GitHub__WebhookSecret", githubWebhookSecret)
    .WithEnvironment("GitHub__AppId", githubAppId)
    .WithEnvironment("GitHub__AppSlug", githubAppSlug)
    .WithEnvironment("GitHub__ClientId", githubClientId)
    .WithEnvironment("GitHub__ClientSecret", githubClientSecret)
    .WithEnvironment("GitHub__PrivateKeyBase64", githubPrivateKeyBase64)
    .WithEnvironment("GitHub__OAuthCallbackUrl", "http://localhost:5173/github/callback")
    .WithEnvironment("RepositoryReasoning__Enabled", repositoryReasoningEnabled)
    .WithEnvironment("RepositoryReasoning__ExecutablePath", repositoryReasoningExecutable)
    .WithEnvironment("RepositoryReasoning__WorkingDirectory", repositoryReasoningWorkingDirectory)
    .WaitFor(database)
    .WaitFor(redis);

if (!string.IsNullOrWhiteSpace(repositoryReasoningModel))
{
    webApi.WithEnvironment("RepositoryReasoning__Model", repositoryReasoningModel);
}

builder.AddNpmApp("frontend", "../../apps/vue", "dev")
    .WithHttpEndpoint(port: 5173, env: "PORT")
    .WithExternalHttpEndpoints()
    .WithReference(webApi)
    .WaitFor(webApi);

using var app = builder.Build();

await app.RunAsync();
