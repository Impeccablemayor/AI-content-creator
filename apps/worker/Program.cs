using Api.Application.AnalyticsSync;
using Api.Application.Publishing;
using Api.Application.Scheduling;
using Api.Application.Security;
using Api.Configuration;
using Api.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using worker;

var environmentName = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT")
    ?? "Development";

LocalDotEnvLoader.LoadIfPresent(environmentName, Directory.GetCurrentDirectory());

var builder = Host.CreateApplicationBuilder(args);

builder.Services
    .AddOptions<DatabaseOptions>()
    .Bind(builder.Configuration.GetSection(DatabaseOptions.SectionName))
    .Validate(
        options => !string.IsNullOrWhiteSpace(options.ConnectionString),
        "Database connection string is required.")
    .ValidateOnStart();

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

builder.Services.AddDbContext<AppDbContext>((serviceProvider, options) =>
{
    var databaseOptions = serviceProvider
        .GetRequiredService<Microsoft.Extensions.Options.IOptions<DatabaseOptions>>()
        .Value;

    options.UseNpgsql(databaseOptions.ConnectionString);
});

// The worker decrypts stored social credentials to publish, so it needs the same
// key ring as the API that encrypted them.
builder.Services.AddSocialCredentialProtection(
    builder.Configuration,
    builder.Environment);
builder.Services.AddSocialPublishing(builder.Configuration);
builder.Services.AddAnalyticsSync(builder.Configuration);

builder.Services.AddScoped<IScheduledPostStore, ScheduledPostDbStore>();
builder.Services.AddScoped<IScheduledPostProcessor, ScheduledPostProcessor>();
builder.Services.AddScoped<IAnalyticsSyncStore, AnalyticsSyncDbStore>();
builder.Services.AddHostedService<ScheduledPostWorkerService>();
builder.Services.AddHostedService<AnalyticsSyncWorkerService>();

var host = builder.Build();
host.Run();
