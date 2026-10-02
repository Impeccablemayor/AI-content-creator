using System.Net;
using System.Text.Json;
using Api.Application.Authentication;
using Api.Application.ContentEvaluation;
using Api.Application.ContentIdeas;
using Api.Application.ContentPillars;
using Api.Application.ContentPosts;
using Api.Application.SocialAccounts;
using Api.Contracts.Responses;
using Api.Application.Analytics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Api.Middleware;

public class GlobalExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionMiddleware> _logger;
    private readonly JsonSerializerOptions _jsonOptions;

    public GlobalExceptionMiddleware(
        RequestDelegate next,
        ILogger<GlobalExceptionMiddleware> logger,
        IOptions<JsonOptions> mvcJsonOptions)
    {
        _next = next;
        _logger = logger;
        _jsonOptions = mvcJsonOptions.Value.JsonSerializerOptions;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Unhandled exception occurred while processing {Method} {Path}",
                context.Request.Method,
                context.Request.Path);

            await HandleExceptionAsync(context, exception);
        }
    }

    private async Task HandleExceptionAsync(
        HttpContext context,
        Exception exception)
    {
        var (statusCode, code, message) = exception switch
        {
            // Every refresh token failure is a 401. The codes stay distinct so a
            // client can tell an expired token from a revoked one, and reuse
            // from a benign retry of a request that lost a race.
            InvalidRefreshTokenException refreshTokenException =>
                (HttpStatusCode.Unauthorized, "INVALID_REFRESH_TOKEN",
                    refreshTokenException.Message),
            ExpiredRefreshTokenException refreshTokenException =>
                (HttpStatusCode.Unauthorized, "REFRESH_TOKEN_EXPIRED",
                    refreshTokenException.Message),
            RevokedRefreshTokenException refreshTokenException =>
                (HttpStatusCode.Unauthorized, "REFRESH_TOKEN_REVOKED",
                    refreshTokenException.Message),
            ReusedRefreshTokenException refreshTokenException =>
                (HttpStatusCode.Unauthorized, "REFRESH_TOKEN_REUSED",
                    refreshTokenException.Message),
            RefreshTokenRotationConflictException refreshTokenException =>
                (HttpStatusCode.Unauthorized, "REFRESH_TOKEN_ROTATION_CONFLICT",
                    refreshTokenException.Message),
            RefreshTokenOwnerInactiveException =>
                (HttpStatusCode.Unauthorized, "ACCOUNT_INACTIVE",
                    "This account is inactive."),
            RefreshTokenWorkspaceAccessRevokedException refreshTokenException =>
                (HttpStatusCode.Unauthorized, "REFRESH_TOKEN_WORKSPACE_ACCESS_REVOKED",
                    refreshTokenException.Message),
            AiProviderUnavailableException =>
                (HttpStatusCode.ServiceUnavailable, "AI_PROVIDER_UNAVAILABLE",
                    "The AI provider is currently unavailable."),
            AiProviderAuthenticationException =>
                (HttpStatusCode.BadGateway, "AI_PROVIDER_AUTHENTICATION_FAILED",
                    "The AI provider could not authenticate the configured credentials."),
            AiProviderRateLimitedException =>
                (HttpStatusCode.ServiceUnavailable, "AI_PROVIDER_RATE_LIMITED",
                    "The AI provider is temporarily rate limited. Try again shortly."),
            AiProviderQuotaExceededException =>
                (HttpStatusCode.ServiceUnavailable, "AI_PROVIDER_QUOTA_EXCEEDED",
                    "The configured AI provider quota is exhausted."),
            AiProviderTimeoutException =>
                (HttpStatusCode.GatewayTimeout, "AI_PROVIDER_TIMEOUT",
                    "The AI provider did not respond in time."),
            AiProviderRequestException =>
                (HttpStatusCode.BadGateway, "AI_PROVIDER_REQUEST_FAILED",
                    "The AI provider could not process the generation request."),
            InvalidAiResponseException =>
                (HttpStatusCode.BadGateway, "AI_RESPONSE_INVALID",
                    "The AI provider returned an invalid response."),
            ContentIdeaContextIncompleteException =>
                (HttpStatusCode.BadRequest, "CONTENT_CONTEXT_INCOMPLETE",
                    "Complete the workspace brand, audience, and strategy before generating ideas."),
            InvalidContentPostRequestException requestException =>
                (HttpStatusCode.BadRequest, "INVALID_CONTENT_POST_REQUEST",
                    requestException.Message),
            ContentIdeaInvalidStatusException =>
                (HttpStatusCode.Conflict, "CONTENT_IDEA_INVALID_STATUS",
                    "Only approved content ideas can be turned into posts."),
            InvalidContentPostScheduleTimeException scheduleTimeException =>
                (HttpStatusCode.BadRequest, "INVALID_CONTENT_POST_SCHEDULE_TIME",
                    scheduleTimeException.Message),
            InvalidContentPostTransitionException transitionException =>
                (HttpStatusCode.Conflict, "INVALID_CONTENT_POST_TRANSITION",
                    transitionException.Message),
            ContentEvaluationFailedException evaluationException =>
                (HttpStatusCode.BadGateway, "CONTENT_EVALUATION_FAILED",
                    evaluationException.Message),
            InvalidContentIdeaRequestException requestException =>
                (HttpStatusCode.BadRequest, "INVALID_CONTENT_IDEA_REQUEST",
                    requestException.Message),
            UnsupportedSocialPlatformException platformException =>
                (HttpStatusCode.BadRequest, "UNSUPPORTED_SOCIAL_PLATFORM",
                    platformException.Message),
            InvalidSocialOAuthStateException stateException =>
                (HttpStatusCode.BadRequest, "INVALID_SOCIAL_OAUTH_STATE",
                    stateException.Message),
            InvalidSocialAccountTransitionException socialTransitionException =>
                (HttpStatusCode.Conflict, "INVALID_SOCIAL_ACCOUNT_TRANSITION",
                    socialTransitionException.Message),
            SocialPlatformNotConfiguredException notConfiguredException =>
                (HttpStatusCode.ServiceUnavailable, "SOCIAL_PLATFORM_NOT_CONFIGURED",
                    notConfiguredException.Message),
            SocialProviderAuthenticationException providerAuthenticationException =>
                (HttpStatusCode.BadGateway, "SOCIAL_PROVIDER_AUTHENTICATION_FAILED",
                    providerAuthenticationException.Message),
            SocialProviderRequestException providerRequestException =>
                (HttpStatusCode.BadGateway, "SOCIAL_PROVIDER_REQUEST_FAILED",
                    providerRequestException.Message),
            InvalidAnalyticsPeriodException periodException =>
                (HttpStatusCode.BadRequest, "INVALID_ANALYTICS_PERIOD",
                    periodException.Message),
            InvalidContentPillarRequestException pillarRequestException =>
                (HttpStatusCode.BadRequest, "INVALID_CONTENT_PILLAR_REQUEST",
                    pillarRequestException.Message),
            ContentPillarAudienceNotFoundException =>
                (HttpStatusCode.BadRequest, "CONTENT_PILLAR_AUDIENCE_NOT_FOUND",
                    "The target audience profile was not found in this workspace."),
            DuplicateContentPillarNameException duplicateNameException =>
                (HttpStatusCode.Conflict, "DUPLICATE_CONTENT_PILLAR_NAME",
                    duplicateNameException.Message),
            _ => (HttpStatusCode.InternalServerError, "INTERNAL_SERVER_ERROR",
                "An unexpected error occurred.")
        };

        context.Response.StatusCode = (int)statusCode;
        context.Response.ContentType = "application/json";

        var response = new ApiErrorResponse
        {
            Error = new ApiError
            {
                Code = code,
                Message = message,
                Details = null
            },
            TraceId = context.TraceIdentifier
        };

        // Serialized with the options MVC itself is configured with, so an error
        // envelope cannot quietly differ in casing from every other response.
        var json = JsonSerializer.Serialize(response, _jsonOptions);

        await context.Response.WriteAsync(json);
    }
}