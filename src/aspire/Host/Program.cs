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

var webApi = builder.AddProject<Projects.Server>("webapi")
    .WithReference(database)
    .WithReference(redis)
    .WithEnvironment("JwtOptions__Key", jwtKey)
    .WithEnvironment("HangfireOptions__Password", hangfirePassword)
    .WithEnvironment("BootstrapAdminOptions__Password", bootstrapAdminPassword)
    .WithEnvironment("GitHub__WebhookSecret", githubWebhookSecret)
    .WaitFor(database)
    .WaitFor(redis);

builder.AddNpmApp("frontend", "../../apps/vue", "dev")
    .WithHttpEndpoint(port: 5173, env: "PORT")
    .WithExternalHttpEndpoints()
    .WithReference(webApi)
    .WaitFor(webApi);

using var app = builder.Build();

await app.RunAsync();
