using Carter;
using Microsoft.AspNetCore.Builder;

namespace VietAIS.TCFlow.WebApi;

public static class Extensions
{
    public static WebApplication MapApi(this WebApplication app)
    {
        var versions = app.NewApiVersionSet().Build();
        var endpoints = app.MapGroup("api/v{version:apiVersion}").WithApiVersionSet(versions);
        endpoints.MapCarter();
        return app;
    }
}
