using Api.Configuration;
using Api.Configuration.Swagger;
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
using Microsoft.AspNetCore.Mvc;
using Microsoft.OpenApi;
using Microsoft.IdentityModel.Tokens;

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
var redisMultiplexer = new Lazy<Task<IConnectionMultiplexer>>(
    () => Task.FromResult<IConnectionMultiplexer>(
        ConnectionMultiplexer.Connect(configuredRedisOptions.ConnectionString)));

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
    options.OperationFilter<AuthorizeOperationFilter>();
});
builder.Services
    .AddHealthChecks()
    .AddCheck<RedisHealthCheck>("redis");

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
    });

builder.Services.AddAuthorization();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<CurrentUserService>();
builder.Services.AddScoped<JwtTokenService>();
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

// Global exception handling
app.UseMiddleware<GlobalExceptionMiddleware>();

// Development tooling
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// HTTP pipeline
app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();