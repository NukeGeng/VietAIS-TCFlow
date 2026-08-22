using FSH.Framework.Infrastructure;
using FSH.Framework.Infrastructure.Logging.Serilog;
using VietAIS.TCFlow.WebApi.Host;
using Serilog;

StaticLogger.EnsureInitialized();
Log.Information("server booting up..");
try
{
    var builder = WebApplication.CreateBuilder(args);

    if (builder.Configuration.GetConnectionString("database") is { Length: > 0 } databaseConnectionString)
    {
        builder.Configuration["DatabaseOptions:ConnectionString"] = databaseConnectionString;
    }

    if (builder.Configuration.GetConnectionString("redis") is { Length: > 0 } redisConnectionString)
    {
        builder.Configuration["CacheOptions:Redis"] = redisConnectionString;
    }

    builder.ConfigureFshFramework();
    builder.RegisterModules();

    var app = builder.Build();

    app.UseFshFramework();
    app.UseModules();
    await app.RunAsync();
}
catch (Exception ex) when (!ex.GetType().Name.Equals("HostAbortedException", StringComparison.Ordinal))
{
    StaticLogger.EnsureInitialized();
    Log.Fatal(ex.Message, "unhandled exception");
}
finally
{
    StaticLogger.EnsureInitialized();
    Log.Information("server shutting down..");
    await Log.CloseAndFlushAsync();
}
