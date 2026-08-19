using Marten;
using Microsoft.AspNetCore.Builder;

namespace VietAIS.TCFlow.WebApi.RepositoryIntelligence;

public static class RepositoryIntelligenceModule
{
    private const string SchemaName = "repository_intelligence";

    public static WebApplicationBuilder RegisterRepositoryIntelligenceServices(
        this WebApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        var connectionString = builder.Configuration["DatabaseOptions:ConnectionString"];
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "DatabaseOptions:ConnectionString is required for Repository Intelligence storage.");
        }

        builder.Services
            .AddMarten(options =>
            {
                options.Connection(connectionString);
                options.DatabaseSchemaName = SchemaName;
            })
            .UseLightweightSessions();

        return builder;
    }

    public static WebApplication UseRepositoryIntelligenceModule(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);
        return app;
    }
}
