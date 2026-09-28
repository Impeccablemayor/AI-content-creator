using Api.Configuration;
using Api.Infrastructure.Persistence;
using Api.Middleware;
using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;
using Api.Infrastructure.Redis;
using System.Text;
using Api.Application.Authentication;
using Api.Application.AI;
using Api.Application.AI.Prompts;
using Api.Application.ContentIdeas;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
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

builder.Services.AddSingleton<IConnectionMultiplexer>(serviceProvider =>
{
    var redisOptions = serviceProvider
        .GetRequiredService<
            Microsoft.Extensions.Options.IOptions<RedisOptions>>()
        .Value;

    return ConnectionMultiplexer.Connect(
        redisOptions.ConnectionString);
});

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
builder.Services.AddSwaggerGen();
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

builder.Services
    .AddOptions<AiOptions>()
    .Bind(builder.Configuration.GetSection(AiOptions.SectionName))
    .Validate(
        options => options.Provider.Equals("Unavailable", StringComparison.OrdinalIgnoreCase) ||
            (options.Provider.Equals("OpenAI", StringComparison.OrdinalIgnoreCase) &&
                !string.IsNullOrWhiteSpace(options.Model) &&
                !string.IsNullOrWhiteSpace(options.ApiKey)),
        "AI provider must be Unavailable or OpenAI; OpenAI requires a model and API key.")
    .Validate(
        options => options.TimeoutSeconds is >= 1 and <= 120,
        "AI timeout must be between 1 and 120 seconds.")
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
var aiProvider = builder.Configuration[$"{AiOptions.SectionName}:Provider"]
    ?? "Unavailable";
if (aiProvider.Equals("OpenAI", StringComparison.OrdinalIgnoreCase))
{
    builder.Services.AddHttpClient<IAiProvider, OpenAiProvider>(
        (serviceProvider, client) =>
        {
            var options = serviceProvider
                .GetRequiredService<Microsoft.Extensions.Options.IOptions<AiOptions>>()
                .Value;
            client.BaseAddress = new Uri("https://api.openai.com/v1/");
            client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
        });
}
else if (aiProvider.Equals("Unavailable", StringComparison.OrdinalIgnoreCase))
{
    builder.Services.AddSingleton<IAiProvider, UnavailableAiProvider>();
}
else
{
    throw new InvalidOperationException(
        "AI:Provider must be set to Unavailable or OpenAI.");
}
builder.Services.AddSingleton<ContentIdeaPromptBuilder>();

var app = builder.Build();

var aiOptions = app.Services
    .GetRequiredService<Microsoft.Extensions.Options.IOptions<AiOptions>>()
    .Value;
app.Logger.LogInformation(
    "AI provider configured: {Provider}; model: {Model}; API key configured: {ApiKeyConfigured}",
    aiOptions.Provider,
    aiOptions.Model,
    !string.IsNullOrWhiteSpace(aiOptions.ApiKey));

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