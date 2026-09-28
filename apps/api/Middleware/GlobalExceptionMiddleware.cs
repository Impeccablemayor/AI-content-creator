using System.Net;
using System.Text.Json;
using Api.Application.ContentIdeas;
using Api.Contracts.Responses;

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
            InvalidContentIdeaRequestException requestException =>
                (HttpStatusCode.BadRequest, "INVALID_CONTENT_IDEA_REQUEST",
                    requestException.Message),
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