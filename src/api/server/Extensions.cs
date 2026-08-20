using System.Reflection;
using Asp.Versioning.Conventions;
using Carter;
using FluentValidation;
using VietAIS.TCFlow.WebApi.Catalog.Application;
using VietAIS.TCFlow.WebApi.Catalog.Infrastructure;
using VietAIS.TCFlow.WebApi.RepositoryIntelligence;
using VietAIS.TCFlow.WebApi.RepositoryIntelligence.Authorization;
using VietAIS.TCFlow.WebApi.RepositoryIntelligence.Management;
using VietAIS.TCFlow.WebApi.Todo;

namespace VietAIS.TCFlow.WebApi.Host;

public static class Extensions
{
    public static WebApplicationBuilder RegisterModules(this WebApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        //define module assemblies
        var assemblies = new Assembly[]
        {
            typeof(CatalogMetadata).Assembly,
            typeof(RepositoryIntelligenceModule).Assembly,
            typeof(TodoModule).Assembly
        };

        //register validators
        builder.Services.AddValidatorsFromAssemblies(assemblies);

        //register mediatr
        builder.Services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssemblies(assemblies);
        });

        //register module services
        builder.RegisterCatalogServices();
        builder.RegisterRepositoryIntelligenceServices();
        builder.RegisterTodoServices();

        //add carter endpoint modules
        builder.Services.AddCarter(configurator: config =>
        {
            config.WithModule<CatalogModule.Endpoints>();
            config.WithModule<ProjectAuthorizationEndpoints>();
            config.WithModule<ProjectGovernanceEndpoints>();
            config.WithModule<ProjectManagementEndpoints>();
            config.WithModule<TodoModule.Endpoints>();
        });

        return builder;
    }

    public static WebApplication UseModules(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        //register modules
        app.UseCatalogModule();
        app.UseRepositoryIntelligenceModule();
        app.UseTodoModule();

        //register api versions
        var versions = app.NewApiVersionSet()
                    .HasApiVersion(1)
                    .HasApiVersion(2)
                    .ReportApiVersions()
                    .Build();

        //map versioned endpoint
        var endpoints = app.MapGroup("api/v{version:apiVersion}").WithApiVersionSet(versions);

        //use carter
        endpoints.MapCarter();

        return app;
    }
}
