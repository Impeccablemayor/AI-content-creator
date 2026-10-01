using System.Net;
using System.Text.Json;
using Api.Application.ContentEvaluation;
using Api.Application.ContentIdeas;
using Api.Application.ContentPillars;
using Api.Application.ContentPosts;
using Api.Application.SocialAccounts;
using Api.Contracts.Responses;
using Api.Application.Analytics;

namespace Api.Middleware;

public class GlobalExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionMiddleware> _logger;

    public GlobalExceptionMiddleware(
        RequestDelegate next,
        ILogger<GlobalExceptionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
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

    private static async Task HandleExceptionAsync(
        HttpContext context,
        Exception exception)
    {
        var (statusCode, code, message) = exception switch
        {
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

        var json = JsonSerializer.Serialize(response);

        await context.Response.WriteAsync(json);
    }
}