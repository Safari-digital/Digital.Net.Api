using Digital.Net.Core.Entities.Context;
using Digital.Net.Core.Http.Accessors;
using Digital.Net.Core.Http.Bootstrap;
using Digital.Net.Core.Http.Endpoints;
using Digital.Net.Core.Http.Security;
using Digital.Net.Core.Http.Services.Authentication;
using Digital.Net.Core.Http.Services.Crud;
using Digital.Net.Core.Http.Services.Documents;
using Digital.Net.Core.Http.Services.Mutations;
using Digital.Net.Lib.Accessors;
using Digital.Net.Lib.Environment;
using Digital.Net.Lib.Validation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Scalar.AspNetCore;

namespace Digital.Net.Core.Http;

public static class CoreHttpInjector
{
    // Above the 10 MB media upload, below Kestrel's ~30 MB default.
    private const long MaxRequestBodyBytes = 12L * 1024 * 1024;

    /// <summary>
    ///     Registers all the Digital.Net HTTP adapters (endpoints, authentication, CRUD). The business layer
    ///     must be registered separately via <c>AddDigitalNetCore()</c>.
    /// </summary>
    public static WebApplicationBuilder AddDigitalNetCoreHttp(this WebApplicationBuilder builder)
    {
        builder.WebHost.ConfigureKestrel(options => options.Limits.MaxRequestBodySize = MaxRequestBodyBytes);

        builder.Services
            .AddHttpContextAccessor()
            .AddScoped<IOriginAccessor, HttpOriginAccessor>()
            .AddCrudServices()
            .AddDigitalAuthenticationServices()
            .AddScoped<DocumentCacheService>()
            .AddSingleton<SseStreamService>()
            .AddSingleton<MutationSignalDispatcher>()
            .AddScoped<IMutationSignalHandler, SseBroadcastHandler>()
            .AddScoped<MutationCatchupReader>()
            .AddScoped<MutationAuditReader>()
            .AddHostedService<RetentionPurgeService>()
            .AddHostedService<MutationStreamListener>()
            .AddRateLimiter(RateLimiter.Options)
            .AddResponseCompression(options => options.EnableForHttps = true)
            .AddOpenApi();

        builder.Services.AddHealthChecks().AddCheck<DatabaseHealthCheck>("database");
        builder.Services.RequireContract<DigitalContext>("AddDigitalNetCore");
        builder
            .SetForwardedHeaders()
            .AddDefaultCorsPolicy();

        return builder;
    }

    /// <summary>
    ///     Wires the Digital.Net HTTP pipeline.
    /// </summary>
    public static WebApplication UseDigitalNetCoreHttp(this WebApplication app)
    {
        // Fail-fast strategy, should always be called first: throws if the business layer
        app.Services.ValidateRequiredContracts();

        app.UseForwardedHeaders();
        app.UseResponseCompression();

        app.Use(async (context, next) =>
        {
            var headers = context.Response.Headers;
            headers.XContentTypeOptions = "nosniff";
            headers.XFrameOptions = "DENY";
            headers["Referrer-Policy"] = "no-referrer";
            headers["Permissions-Policy"] = "geolocation=(), microphone=(), camera=()";

            if (context.Request.IsHttps)
                headers.StrictTransportSecurity = "max-age=31536000; includeSubDomains";
            if (!AspNetEnv.IsDevelopment)
                headers.ContentSecurityPolicy = "default-src 'none'; frame-ancestors 'none'";
            
            await next();
        });

        app.UseCors();
        app.UseStaticFiles();

        if (!AspNetEnv.IsTest)
            app.UseRateLimiter();

        app.MapHealthChecks("/health");

        app
            .MapRootEndpoints()
            .MapAuthenticationEndpoints()
            .MapUserEndpoints()
            .MapApiKeyEndpoints()
            .MapValidationEndpoints()
            .MapEntityMutationEndpoints();

        if (AspNetEnv.IsDevelopment)
        {
            app.MapScalarApiReference();
            app.MapOpenApi();
        }

        return app;
    }
}