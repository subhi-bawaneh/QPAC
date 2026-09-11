using System.Reflection;
using System.Text;
using System.Threading.RateLimiting;
using Dip.Api.Common;
using Dip.Application.Abstractions;
using Dip.Application.Behaviors;
using Dip.Api.Health;
using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

namespace Dip.Api;

public static class DependencyInjection
{
    /// Applied to the login and refresh endpoints with [EnableRateLimiting].
    public const string AuthRateLimitPolicy = "auth";

    public static IServiceCollection AddApi(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddControllers()
            .AddJsonOptions(options =>
            {
                // Serialize/deserialize enums as strings so clients see "Draft" / "Live"
                // instead of 0 / 1. Also matches the JSON in integration tests and Swagger docs.
                options.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
            });
        services.AddEndpointsApiExplorer();
        services.AddHttpContextAccessor();
        services.AddExceptionHandler<ProblemDetailsExceptionHandler>();
        services.AddProblemDetails();
        // /health is the liveness probe an uptime monitor polls: process + database.
        services.AddHealthChecks()
            .AddCheck<DatabaseHealthCheck>(DatabaseHealthCheck.Name, tags: ["core"]);

        services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc("v1", new OpenApiInfo { Title = "DIP API", Version = "v1" });
            options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
            {
                Name = "Authorization",
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                In = ParameterLocation.Header,
            });
            options.AddSecurityRequirement(new OpenApiSecurityRequirement
            {
                {
                    new OpenApiSecurityScheme
                    {
                        Reference = new OpenApiReference
                        {
                            Type = ReferenceType.SecurityScheme,
                            Id = "Bearer",
                        },
                    },
                    Array.Empty<string>()
                },
            });
        });

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                // Fall back to a stable dev-only key when the config value is missing or empty
                // (integration tests, first-run local dev). Production must set Jwt:Key via env var.
                var configuredKey = configuration["Jwt:Key"];
                var key = string.IsNullOrWhiteSpace(configuredKey)
                    ? "dev-placeholder-key-change-me-please-32b-min"
                    : configuredKey;
                var issuer = configuration["Jwt:Issuer"] ?? "dip";
                var audience = configuration["Jwt:Audience"] ?? "dip";

                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = issuer,
                    ValidAudience = audience,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)),
                    ClockSkew = TimeSpan.FromMinutes(1),
                };

                // SignalR's WebSocket handshake cannot set an Authorization header,
                // so the hub client passes the token in the query string instead.
                options.Events = new JwtBearerEvents
                {
                    OnMessageReceived = context =>
                    {
                        var accessToken = context.Request.Query["access_token"];
                        if (!string.IsNullOrEmpty(accessToken)
                            && context.HttpContext.Request.Path.StartsWithSegments("/hubs"))
                        {
                            context.Token = accessToken;
                        }
                        return Task.CompletedTask;
                    },
                };
            });

        services.AddAuthorization();

        var corsOrigins = (configuration["Cors:Origins"] ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        // Outside Development a missing Cors:Origins used to fall back to "any origin",
        // which turns one forgotten Plesk variable into an API every website may call.
        // Fail at startup instead: a boot error is found immediately, an open CORS
        // policy is not.
        var isDevelopment = string.Equals(
            configuration["ASPNETCORE_ENVIRONMENT"] ?? Environments.Development,
            Environments.Development,
            StringComparison.OrdinalIgnoreCase);

        if (corsOrigins.Length == 0 && !isDevelopment)
        {
            throw new InvalidOperationException(
                "Cors:Origins must list the frontend origins outside Development, "
                + "e.g. Cors__Origins=https://dip.vercel.app");
        }

        // Vercel gives every preview deployment its own hostname, and WithOrigins has no
        // wildcards, so previews are matched by suffix when the operator opts in.
        var previewSuffix = configuration["Cors:PreviewOriginSuffix"];

        services.AddCors(options =>
        {
            options.AddDefaultPolicy(policy =>
            {
                if (corsOrigins.Length == 0)
                {
                    // Development only, by the guard above. Not AllowAnyOrigin: the
                    // SignalR negotiate call sends credentials, which "*" forbids.
                    policy.WithOrigins("http://localhost:5173")
                        .AllowAnyHeader().AllowAnyMethod().AllowCredentials();
                    return;
                }

                policy.WithOrigins(corsOrigins).AllowAnyHeader().AllowAnyMethod().AllowCredentials();

                if (!string.IsNullOrWhiteSpace(previewSuffix))
                {
                    policy.SetIsOriginAllowed(origin =>
                        corsOrigins.Contains(origin, StringComparer.Ordinal)
                        || (Uri.TryCreate(origin, UriKind.Absolute, out var uri)
                            && uri.Scheme == Uri.UriSchemeHttps
                            && uri.Host.EndsWith(previewSuffix, StringComparison.OrdinalIgnoreCase)));
                }
            });
        });

        // Auth is the one unauthenticated surface, so it is the one worth limiting:
        // a fixed window per client address, generous enough that a person retyping a
        // password never meets it.
        var authPermitPerWindow = configuration.GetValue("RateLimit:AuthPermitPerWindow", 20);
        var authWindowSeconds = configuration.GetValue("RateLimit:AuthWindowSeconds", 60);

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.AddPolicy(AuthRateLimitPolicy, context =>
                RateLimitPartition.GetFixedWindowLimiter(
                    context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = authPermitPerWindow,
                        Window = TimeSpan.FromSeconds(authWindowSeconds),
                        QueueLimit = 0,
                    }));
        });

        services.AddScoped<IDispatcher, Dispatcher.Dispatcher>();
        services.AddScoped<ICurrentUser, CurrentUser>();
        services.AddScoped<Features.Recalculation.RecalculationService>();

        // Background pipeline: a process-wide queue and status, scoped services that
        // do the work, and the two hosted workers that drive them (decision D4).
        services.AddSignalR();
        services.AddSingleton<Workers.WorkQueue>();
        services.AddSingleton<Hubs.ISyncNotifier, Hubs.HubSyncNotifier>();
        services.AddHostedService<Workers.ImportWorker>();

        // Pipeline behaviors — order matters: Logging first, Auth second, then Validation, then Transaction (commands only).
        services.AddScoped(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));
        services.AddScoped(typeof(IPipelineBehavior<,>), typeof(AuthorizationBehavior<,>));
        services.AddScoped(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
        services.AddScoped(typeof(IPipelineBehavior<,>), typeof(TransactionBehavior<,>));

        RegisterHandlersAndValidators(services);
        return services;
    }

    private static void RegisterHandlersAndValidators(IServiceCollection services)
    {
        var assembly = Assembly.GetExecutingAssembly();

        services.Scan(scan => scan
            .FromAssemblies(assembly)
            .AddClasses(c => c.AssignableTo(typeof(ICommandHandler<,>)))
            .AsImplementedInterfaces()
            .WithScopedLifetime()
            .AddClasses(c => c.AssignableTo(typeof(IQueryHandler<,>)))
            .AsImplementedInterfaces()
            .WithScopedLifetime());

        services.AddValidatorsFromAssembly(assembly);
    }
}
