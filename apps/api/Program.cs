using Api.Configuration;
using Api.Configuration.Swagger;
using Api.Infrastructure.Health;
using Api.Infrastructure.Metrics;
using Api.Infrastructure.Persistence;
using Api.Middleware;
using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;
using Api.Infrastructure.Redis;
using System.Text;
using Api.Application.Authentication;
using Api.Application.AI;
using Api.Application.AI.Prompts;
using Api.Application.Analytics;
using Api.Application.AuditLogs;
using Api.Application.ContentEvaluation;
using Api.Application.ContentIdeas;
using Api.Application.ContentPillars;
using Api.Application.Learning;
using Api.Application.Scheduling;
using Api.Application.Security;
using Api.Application.Services;
using Api.Application.SocialAccounts;
using Api.Contracts.ContentPost;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.OpenApi;
using Microsoft.IdentityModel.Tokens;

/// <summary>Named CORS policy the pipeline applies.</summary>
const string CorsPolicyName = "ApiCorsPolicy";

var environmentName = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT")
    ?? Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
    ?? "Production";

LocalDotEnvLoader.LoadIfPresent(environmentName, Directory.GetCurrentDirectory());

var builder = WebApplication.CreateBuilder(args);

// Configuration
builder.Services
    .AddOptions<DatabaseOptions>()
    .Bind(builder.Configuration.GetSection(DatabaseOptions.SectionName))
    .Validate(
        options => !string.IsNullOrWhiteSpace(options.ConnectionString),
        "Database connection string is required.")
    .ValidateOnStart();

// Database
builder.Services.AddDbContext<AppDbContext>((serviceProvider, options) =>
{
    var databaseOptions = serviceProvider
        .GetRequiredService<
            Microsoft.Extensions.Options.IOptions<DatabaseOptions>>()
        .Value;

    options.UseNpgsql(databaseOptions.ConnectionString);
});

builder.Services
    .AddOptions<RedisOptions>()
    .Bind(builder.Configuration.GetSection(RedisOptions.SectionName))
    .Validate(
        options => !string.IsNullOrWhiteSpace(options.ConnectionString),
        "Redis connection string is required.")
    .ValidateOnStart();

// One lazily created Redis connection, shared by the health check and the
// distributed cache used for single-use OAuth state.
var configuredRedisOptions = builder.Configuration
    .GetSection(RedisOptions.SectionName)
    .Get<RedisOptions>()
    ?? new RedisOptions();

// AbortOnConnectFail is off unless the connection string says otherwise. Left at
// the library default of true, Connect throws the first time it is resolved while
// Redis is down, which escapes the health check as a 500 instead of the 503 the
// probe needs, and turns a cache outage into an error page. With it off the
// multiplexer keeps retrying in the background and RedisHealthCheck reports the
// outage through its own result, which is what lets the rest of the API keep
// serving the requests that do not need the cache.
var redisConfiguration = ConfigurationOptions.Parse(
    configuredRedisOptions.ConnectionString);

if (!configuredRedisOptions.ConnectionString.Contains(
        "abortConnect",
        StringComparison.OrdinalIgnoreCase))
{
    redisConfiguration.AbortOnConnectFail = false;
}

var redisMultiplexer = new Lazy<Task<IConnectionMultiplexer>>(
    () => Task.FromResult<IConnectionMultiplexer>(
        ConnectionMultiplexer.Connect(redisConfiguration)));

builder.Services.AddSingleton<IConnectionMultiplexer>(_ =>
    redisMultiplexer.Value.GetAwaiter().GetResult());

// Shared cache for short-lived server state such as single-use OAuth state.
builder.Services.AddStackExchangeRedisCache(options =>
    options.ConnectionMultiplexerFactory = () => redisMultiplexer.Value);

// Framework services
builder.Services
    .AddControllers()
    .ConfigureApiBehaviorOptions(options =>
    {
        options.InvalidModelStateResponseFactory = context =>
        {
            var errors = context.ModelState
                .Where(entry => entry.Value?.Errors.Count > 0)
                .ToDictionary(
                    entry => entry.Key,
                    entry => entry.Value!.Errors
                        .Select(error => error.ErrorMessage)
                        .ToArray());

            return new BadRequestObjectResult(new Api.Contracts.Responses.ApiErrorResponse
            {
                Error = new Api.Contracts.Responses.ApiError
                {
                    Code = "INVALID_REQUEST",
                    Message = "The request is invalid.",
                    Details = errors
                },
                TraceId = context.HttpContext.TraceIdentifier
            });
        };
    });
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "JWT bearer access token."
    });

    // How a browser actually authenticates. The cookie is documented rather
    // than the header alone, because a generated client built only from the
    // header would push a token into JavaScript and reintroduce exactly what the
    // cookie avoids.
    options.AddSecurityDefinition(
        AuthCookieOptions.OpenApiSecuritySchemeName,
        new OpenApiSecurityScheme
        {
            Name = AuthCookieOptions.DefaultAccessTokenCookieName,
            Type = SecuritySchemeType.ApiKey,
            In = ParameterLocation.Cookie,
            Description =
                "httpOnly session cookie set by register, login, refresh, " +
                "workspace create, and workspace switch. Sent automatically " +
                "by the browser; page script cannot read it."
        });

    options.OperationFilter<AuthorizeOperationFilter>();
});
// The readiness checks run against the dependencies the API cannot serve traffic
// without. AI provider configuration is included but cannot fail the endpoint: see
// AiProviderReadinessCheck for why it must not make a network call.
builder.Services
    .AddHealthChecks()
    .AddCheck<PostgresHealthCheck>(
        "postgres",
        tags: ["ready", "database"])
    .AddCheck<RedisHealthCheck>(
        "redis",
        tags: ["ready", "cache"])
    .AddCheck<AiProviderReadinessCheck>(
        "ai-provider",
        tags: ["ready"]);

builder.Services
    .AddOptions<JwtOptions>()
    .Bind(builder.Configuration.GetSection(JwtOptions.SectionName))
    .Validate(
        options => !string.IsNullOrWhiteSpace(options.SecretKey),
        "JWT secret key is required.")
    .Validate(
        options => options.SecretKey.Length >= 32,
        "JWT secret key must be at least 32 characters.")
    .ValidateOnStart();

builder.Services.AddConfiguredAiProvider(builder.Configuration);

builder.Services
    .AddOptions<ContentQualityOptions>()
    .Bind(builder.Configuration.GetSection(ContentQualityOptions.SectionName))
    .Validate(
        options => options.PassThreshold is >= 0m and <= 100m,
        "Content quality pass threshold must be between 0 and 100.")
    .Validate(
        options => options.RepetitionSimilarityThreshold is >= 0m and <= 1m,
        "Content quality repetition similarity threshold must be between 0 and 1.")
    .ValidateOnStart();

var jwtOptions = builder.Configuration
    .GetSection(JwtOptions.SectionName)
    .Get<JwtOptions>()
    ?? throw new InvalidOperationException(
        "JWT configuration is missing.");

// The browser session is an httpOnly cookie pair. Whether those cookies carry
// Secure and which SameSite they need is a property of the deployed topology,
// not of this code, so it is configuration with a safe default rather than a
// constant chosen here.
var authCookieSection = builder.Configuration
    .GetSection(AuthCookieOptions.SectionName);

builder.Services
    .AddOptions<AuthCookieOptions>()
    .Bind(authCookieSection)
    .PostConfigure(options =>
    {
        // Configuration is consulted for the environment-sensitive default
        // because it cannot live in the options object: it depends on which
        // environment the host is in. The rule itself lives on the options type
        // so it can be asserted directly.
        options.Secure = AuthCookieOptions.ResolveSecure(
            authCookieSection[nameof(AuthCookieOptions.Secure)]
                is string configured && bool.TryParse(configured, out var parsed)
                    ? parsed
                    : null,
            builder.Environment.IsDevelopment());
    })
    // A browser silently discards SameSite=None without Secure, so a deployment
    // that asked for it would appear to work in the API's own logs and fail for
    // every real user. Refusing to start names the problem instead.
    .Validate(
        options => options.HasAcceptableSameSiteConfiguration(),
        "AuthCookies:SameSite=None requires AuthCookies:Secure=true, because " +
        "browsers reject a SameSite=None cookie that is not Secure.")
    .Validate(
        options => options.HasRequiredCookieNames(),
        "Both AuthCookies cookie names are required.")
    .ValidateOnStart();

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,

            ValidIssuer = jwtOptions.Issuer,
            ValidAudience = jwtOptions.Audience,

            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(jwtOptions.SecretKey)),

            ClockSkew = TimeSpan.FromSeconds(30)
        };

        // A browser sends the access token in a cookie instead of a header,
        // because the only way to keep it out of reach of page script is to
        // never hand it to page script. Validation is untouched: it is the same
        // token, the same issuer, the same audience, and the same signing key.
        //
        // A header that is present is left alone. The header is what a
        // non-browser client sends, and quietly preferring the cookie would let
        // a stale cookie override a token the caller deliberately supplied.
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                if (string.IsNullOrEmpty(context.Token) &&
                    context.Request.Headers.ContainsKey(
                        "Authorization"))
                {
                    return Task.CompletedTask;
                }

                if (string.IsNullOrEmpty(context.Token))
                {
                    var cookieValue = context.HttpContext.Request.Cookies[
                        context.HttpContext.RequestServices
                            .GetRequiredService<
                                Microsoft.Extensions.Options
                                    .IOptions<AuthCookieOptions>>()
                            .Value
                            .AccessTokenCookieName];

                    if (!string.IsNullOrWhiteSpace(cookieValue))
                    {
                        context.Token = cookieValue;
                    }
                }

                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization();

builder.Services
    .AddOptions<RateLimitingOptions>()
    .Bind(builder.Configuration.GetSection(RateLimitingOptions.SectionName))
    .Validate(
        options => options.AuthenticationPermitLimit >= 0,
        "Rate limiting permit limit must not be negative.")
    .Validate(
        options => options.AuthenticationWindow
            > TimeSpan.Zero,
        "Rate limiting window must be positive.")
    .ValidateOnStart();

// Origins come from configuration only. No wildcard and no built-in default
// origin: the repository has no frontend to infer one from, and shipping a
// guessed development origin into production would be a silent weakening.
// Outside Development an empty list is a configuration mistake, so it stops
// startup rather than quietly allowing every origin.
builder.Services
    .AddOptions<CorsOptions>()
    .Bind(builder.Configuration.GetSection(CorsOptions.SectionName));

var allowedOrigins = builder.Configuration
    .GetSection(CorsOptions.SectionName)
    .Get<CorsOptions>()?
    .AllowedOrigins?
    .Where(origin => !string.IsNullOrWhiteSpace(origin))
    .Select(origin => origin.Trim())
    .Distinct(StringComparer.OrdinalIgnoreCase)
    .ToArray()
    ?? [];

if (allowedOrigins.Length == 0 && !builder.Environment.IsDevelopment())
{
    throw new InvalidOperationException(
        $"No CORS origins are configured. Set " +
        $"'{CorsOptions.SectionName}:AllowedOrigins' to the exact frontend " +
        "origins permitted to call this API, or run in Development.");
}

builder.Services.AddCors(options =>
{
    options.AddPolicy(CorsPolicyName, policy =>
    {
        if (allowedOrigins.Length == 0)
        {
            // Development with nothing configured: no browser origin is
            // permitted, which is the same position as production.
            policy.WithOrigins([]);
        }
        else
        {
            policy
                .WithOrigins(allowedOrigins)
                .AllowAnyHeader()
                .AllowAnyMethod()
                .AllowCredentials();
        }
    });
});

// Metrics are read at startup, alongside the other settings that decide what the
// API serves rather than only how it behaves. Disabled means the route and the
// listener behind it are both absent, so nothing is collected for an endpoint
// nobody reads.
//
// No ValidateOnStart here: a boolean has no invalid state to fail fast on.
builder.Services
    .AddOptions<MetricsOptions>()
    .Bind(builder.Configuration.GetSection(MetricsOptions.SectionName));

var metricsEnabled = builder.Configuration
    .GetSection(MetricsOptions.SectionName)
    .Get<MetricsOptions>()?
    .Enabled
    ?? new MetricsOptions().Enabled;

if (metricsEnabled)
{
    // Singleton: one listener holds the process totals for the lifetime of the
    // process, and every scrape reads the same snapshot.
    builder.Services.AddSingleton<PrometheusMetricsCollector>();
}

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<CurrentUserService>();
builder.Services.AddScoped<JwtTokenService>();
builder.Services.AddScoped<IRefreshTokenService, RefreshTokenService>();
builder.Services.AddScoped<IAuthCookieService, AuthCookieService>();
builder.Services.AddScoped<IContentIdeaService, ContentIdeaService>();
builder.Services.AddScoped<IContentPillarService, ContentPillarService>();
builder.Services.AddScoped<IAuditLogService, AuditLogService>();
builder.Services
    .AddOptions<SchedulingOptions>()
    .Bind(builder.Configuration.GetSection(SchedulingOptions.SectionName))
    .Validate(
        options => options.PollIntervalSeconds is >= 1 and <= 3600,
        "Scheduling poll interval must be between 1 and 3600 seconds.")
    .Validate(
        options => options.BatchSize is >= 1 and <= 500,
        "Scheduling batch size must be between 1 and 500.")
    .Validate(
        options => options.ClaimExpiryMinutes is >= 1 and <= 1440,
        "Scheduling claim expiry must be between 1 and 1440 minutes.")
    .ValidateOnStart();

builder.Services.AddScoped<IContentPostService, ContentPostService>();
builder.Services.AddScoped<IAiRunRecorder, AiRunRecorder>();
builder.Services.AddSingleton<ContentIdeaPromptBuilder>();
builder.Services.AddSingleton<ContentPostPromptBuilder>();
builder.Services.AddSingleton<ContentEvaluationPromptBuilder>();
builder.Services.AddSingleton<ContentRepetitionDetector>();

// Scheduling and publishing are deliberately absent here. The worker owns both:
// it is the only host that needs AddSocialPublishing, and the only host that
// runs ScheduledPostWorkerService. Registering the processor in the API created
// a dependency on IScheduledPostPublishService, which only the worker registers,
// so the API could not build. Nothing in the API resolves either interface, so
// nothing is lost by leaving them to the worker.

builder.Services.AddScoped<IPublishedPostMetricsReader, PublishedPostMetricsReader>();
builder.Services.AddScoped<IAnalyticsService, AnalyticsService>();
builder.Services.AddLearning(builder.Configuration);

builder.Services.AddSocialCredentialProtection(
    builder.Configuration,
    builder.Environment);
builder.Services.AddConfiguredSocialPlatformProviders(builder.Configuration);

var app = builder.Build();

var aiOptions = app.Services
    .GetRequiredService<Microsoft.Extensions.Options.IOptions<AiOptions>>()
    .Value;
var aiProvider = app.Services.GetRequiredService<IAiProvider>();
app.Logger.LogInformation(
    "AI provider configured: {Provider}; implementation: {ProviderImplementation}; " +
    "model: {Model}; API key configured: {ApiKeyConfigured}",
    aiOptions.Provider,
    aiProvider.GetType().Name,
    aiOptions.Model,
    !string.IsNullOrWhiteSpace(aiOptions.ApiKey));
if (aiOptions.Provider.Equals("Unavailable", StringComparison.OrdinalIgnoreCase) &&
    !string.IsNullOrWhiteSpace(aiOptions.Model) &&
    !string.IsNullOrWhiteSpace(aiOptions.ApiKey))
{
    app.Logger.LogWarning(
        "AI__Provider is effectively Unavailable although a model and API key are configured. " +
        "Check AI__Provider in the Development .env file; its value overrides inherited process environment variables.");
}

// Correlation runs first so every later stage, including the exception handler,
// logs and reports under one identifier.
app.UseMiddleware<CorrelationIdMiddleware>();

// Counts and times every request. It runs immediately after correlation and
// outside the exception handler, so nothing can short-circuit past it and a
// request that ended in an exception is counted with the status that was
// returned.
app.UseMiddleware<HttpMetricsMiddleware>();

// Oversized bodies are refused before anything reads them. The ceiling itself is
// read by the middleware from RequestSizeLimit:MaxRequestBodySizeInBytes.
app.UseMiddleware<RequestSizeLimitMiddleware>();

// Global exception handling, then the response headers every response carries.
app.UseMiddleware<GlobalExceptionMiddleware>();
app.UseMiddleware<SecurityHeadersMiddleware>();

// Development tooling, off everywhere else unless explicitly enabled.
if (SwaggerExposure.ShouldServe(app.Environment, app.Configuration))
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// HTTP pipeline
app.UseHttpsRedirection();

// CORS answers preflights and stamps allowed origins on real responses.
app.UseCors(CorsPolicyName);

// Throttling sits after CORS so a rejected preflight is not itself throttled.
app.UseMiddleware<RateLimitingMiddleware>();

app.UseAuthentication();

// The request log runs after authentication so the caller is known, and before
// authorization so a rejected request is still logged.
app.UseMiddleware<RequestLoggingMiddleware>();

app.UseAuthorization();

// Liveness answers only whether this process can serve a request. It runs no
// dependency check on purpose: if it did, a database outage would have an
// orchestrator restarting a process that is otherwise perfectly able to answer,
// which turns a recoverable dependency failure into an outage.
app.MapHealthChecks("/api/v1/health/live", new HealthCheckOptions
{
    Predicate = _ => false,
    ResponseWriter = HealthResponseWriter.WriteAsync
});

// Readiness runs the dependency checks. A dependency that is down answers 503, so
// the response describes the system instead of asserting it is fine.
app.MapHealthChecks("/api/v1/health", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready"),
    ResponseWriter = HealthResponseWriter.WriteAsync
});

// The scrape endpoint. Anonymous on purpose: a scraper is not a workspace caller
// and holds no token. Excluded from the OpenAPI document because it is not part
// of the API a client integrates against.
if (metricsEnabled)
{
    // Resolved once here, while the host is still being built, rather than left to
    // be created by the first scrape. A listener only sees measurements made after
    // it starts, so a collector created on the first request would report a process
    // that had handled nothing: every request before it, and every AI run and
    // background scan since startup, would be missing from the first scrape.
    _ = app.Services.GetRequiredService<PrometheusMetricsCollector>();

    app.MapGet(MetricsScrapeEndpoint.Route, MetricsScrapeEndpoint.WriteAsync)
        .ExcludeFromDescription();
}

app.MapControllers();

app.Run();

/// <summary>
/// Named so the integration test project can host this exact composition root through
/// <c>WebApplicationFactory&lt;Program&gt;</c>. Without it, a top-level program is
/// internal and the tests would have to rebuild the container, which is the part
/// under test.
/// </summary>
public partial class Program
{
}
