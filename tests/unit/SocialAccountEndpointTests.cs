using System.Reflection;
using System.Text.Json;
using Api.Application.SocialAccounts;
using Api.Contracts.Responses;
using Api.Contracts.Social;
using Api.Controllers;
using Api.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Xunit;

namespace Api.Tests;

public sealed class SocialAccountEndpointTests
{
    [Fact]
    public void Social_controller_is_scoped_to_the_social_route()
    {
        var controller = typeof(SocialAccountsController);

        Assert.Equal("api/v1/social", controller.GetCustomAttribute<RouteAttribute>()?.Template);
    }

    [Theory]
    [InlineData(nameof(SocialAccountsController.GetAccounts), "accounts", "GET")]
    [InlineData(nameof(SocialAccountsController.Connect), "{platform}/connect", "GET")]
    [InlineData(nameof(SocialAccountsController.Callback), "{platform}/callback", "GET")]
    [InlineData(nameof(SocialAccountsController.Disconnect), "accounts/{id:guid}", "DELETE")]
    public void Social_actions_expose_the_documented_verbs_and_templates(
        string actionName,
        string expectedTemplate,
        string expectedVerb)
    {
        var action = typeof(SocialAccountsController)
            .GetMethods()
            .Single(method => method.Name == actionName);

        var isCallback = actionName == nameof(SocialAccountsController.Callback);
        if (isCallback)
        {
            Assert.NotNull(action.GetCustomAttribute<AllowAnonymousAttribute>());
        }
        else
        {
            Assert.NotNull(action.GetCustomAttribute<AuthorizeAttribute>());
            Assert.Null(action.GetCustomAttribute<AllowAnonymousAttribute>());
        }

        var template = action.GetCustomAttribute<HttpGetAttribute>()?.Template
            ?? action.GetCustomAttribute<HttpDeleteAttribute>()?.Template;

        Assert.Equal(expectedTemplate, template);
        var usesExpectedVerb = expectedVerb == "GET"
            ? action.GetCustomAttribute<HttpGetAttribute>() is not null
            : action.GetCustomAttribute<HttpDeleteAttribute>() is not null;

        Assert.True(usesExpectedVerb, $"{actionName} should be an {expectedVerb}.");
    }

    [Fact]
    public void Only_the_oauth_callback_is_anonymous()
    {
        var actions = typeof(SocialAccountsController)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(method => method.GetCustomAttributes<HttpMethodAttribute>().Any())
            .ToList();

        Assert.Equal(4, actions.Count);
        var callback = actions.Single(action => action.Name == nameof(SocialAccountsController.Callback));

        Assert.NotNull(callback.GetCustomAttribute<AllowAnonymousAttribute>());
        Assert.All(
            actions.Where(action => action.Name != nameof(SocialAccountsController.Callback)),
            action => Assert.Null(action.GetCustomAttribute<AllowAnonymousAttribute>()));
    }

    [Fact]
    public void The_callback_takes_its_oauth_inputs_from_the_query_string()
    {
        var callback = typeof(SocialAccountsController)
            .GetMethod(nameof(SocialAccountsController.Callback));
        var parameterNames = callback!.GetParameters().Select(parameter => parameter.Name).ToArray();

        Assert.Contains("code", parameterNames);
        Assert.Contains("state", parameterNames);
        Assert.Contains("error", parameterNames);
        Assert.All(
            callback.GetParameters()
                .Where(parameter => parameter.Name is "code" or "state" or "error"),
            parameter => Assert.NotNull(parameter.GetCustomAttribute<FromQueryAttribute>()));
    }

    /// <summary>
    /// The callback is the only unauthenticated write in the API, so the shape of
    /// what it returns is the last line of defence against leaking credentials.
    /// </summary>
    [Fact]
    public void The_social_response_contract_has_no_credential_properties()
    {
        foreach (var type in new[] { typeof(SocialAccountResponse), typeof(SocialConnectResponse) })
        {
            var propertyNames = type.GetProperties().Select(property => property.Name).ToArray();

            Assert.DoesNotContain(
                propertyNames,
                name => name.Contains("AccessToken", StringComparison.OrdinalIgnoreCase) ||
                    name.Contains("RefreshToken", StringComparison.OrdinalIgnoreCase) ||
                    name.Contains("Secret", StringComparison.OrdinalIgnoreCase) ||
                    name.Contains("Encrypted", StringComparison.OrdinalIgnoreCase) ||
                    name.Contains("CodeVerifier", StringComparison.OrdinalIgnoreCase));
        }
    }

    [Fact]
    public void The_callback_response_serialises_without_any_credential()
    {
        var account = new SocialAccountResponse
        {
            Id = Guid.NewGuid(),
            Platform = "X",
            Username = "mayor",
            DisplayName = "Mayor",
            ProfileUrl = "https://x.com/mayor",
            Status = "Connected",
            TokenExpiresAt = DateTime.UtcNow.AddHours(1),
            ConnectedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        var json = JsonSerializer.Serialize(
            new ApiResponse<SocialAccountResponse> { Data = account });

        Assert.DoesNotContain("access_token", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("refresh_token", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("secret", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("code_verifier", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("AccessToken", json, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Connect_returns_the_authorization_url_in_the_success_envelope()
    {
        var service = new StubSocialAccountService
        {
            ConnectResult = new SocialConnectResponse
            {
                Platform = "LinkedIn",
                AuthorizationUrl = "https://provider.example.com/authorize?state=abc",
                ExpiresAt = DateTime.UtcNow.AddMinutes(10)
            }
        };
        var controller = new SocialAccountsController(service);

        var result = await controller.Connect("linkedin", CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var envelope = Assert.IsType<ApiResponse<SocialConnectResponse>>(ok.Value);
        Assert.Equal("linkedin", service.LastPlatform);
        Assert.Equal("https://provider.example.com/authorize?state=abc", envelope.Data!.AuthorizationUrl);
    }

    [Fact]
    public async Task Listing_returns_every_connected_account_in_the_envelope()
    {
        var accounts = new List<SocialAccountResponse>
        {
            new() { Id = Guid.NewGuid(), Platform = "LinkedIn", Status = "Connected" }
        };
        var controller = new SocialAccountsController(
            new StubSocialAccountService { Accounts = accounts });

        var result = await controller.GetAccounts(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var envelope = Assert.IsType<ApiResponse<List<SocialAccountResponse>>>(ok.Value);
        Assert.Single(envelope.Data!);
    }

    [Fact]
    public async Task A_denied_authorization_returns_a_bad_request_without_touching_the_service()
    {
        var service = new StubSocialAccountService();
        var controller = new SocialAccountsController(service);

        var result = await controller.Callback(
            "linkedin",
            code: null,
            state: "state-value",
            error: "user_cancelled_login",
            CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        var error = Assert.IsType<ApiErrorResponse>(badRequest.Value);
        Assert.Equal("SOCIAL_OAUTH_DENIED", error.Error!.Code);
        Assert.Null(service.LastCompletedCode);
    }

    [Fact]
    public async Task The_callback_passes_the_state_to_the_service_which_resolves_the_workspace()
    {
        var service = new StubSocialAccountService
        {
            CompleteResult = new SocialAccountResponse
            {
                Id = Guid.NewGuid(),
                Platform = "LinkedIn",
                Status = "Connected"
            }
        };
        var controller = new SocialAccountsController(service);

        var result = await controller.Callback(
            "linkedin",
            code: "auth-code",
            state: "state-value",
            error: null,
            CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var envelope = Assert.IsType<ApiResponse<SocialAccountResponse>>(ok.Value);
        Assert.Equal("state-value", service.LastCompletedState);
        Assert.Equal("auth-code", service.LastCompletedCode);
        Assert.NotNull(envelope.Data);
    }

    [Fact]
    public async Task Disconnecting_an_unknown_account_returns_not_found()
    {
        var controller = new SocialAccountsController(
            new StubSocialAccountService { DisconnectResult = null });

        var result = await controller.Disconnect(Guid.NewGuid(), CancellationToken.None);

        var notFound = Assert.IsType<NotFoundObjectResult>(result);
        var error = Assert.IsType<ApiErrorResponse>(notFound.Value);
        Assert.Equal("SOCIAL_ACCOUNT_NOT_FOUND", error.Error!.Code);
    }

    [Fact]
    public async Task Disconnecting_returns_the_destroyed_account_in_the_envelope()
    {
        var disconnected = new SocialAccountResponse
        {
            Id = Guid.NewGuid(),
            Platform = "LinkedIn",
            Status = "Disconnected"
        };
        var service = new StubSocialAccountService { DisconnectResult = disconnected };
        var controller = new SocialAccountsController(service);
        var id = Guid.NewGuid();

        var result = await controller.Disconnect(id, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var envelope = Assert.IsType<ApiResponse<SocialAccountResponse>>(ok.Value);
        Assert.Equal(id, service.LastDisconnectedId);
        Assert.Equal("Disconnected", envelope.Data!.Status);
    }

    private sealed class StubSocialAccountService : ISocialAccountService
    {
        public IReadOnlyList<SocialAccountResponse> Accounts { get; init; } = [];

        public SocialConnectResponse? ConnectResult { get; init; }

        public SocialAccountResponse? CompleteResult { get; init; }

        public SocialAccountResponse? DisconnectResult { get; init; }

        public string? LastPlatform { get; private set; }

        public string? LastCompletedCode { get; private set; }

        public string? LastCompletedState { get; private set; }

        public Guid? LastDisconnectedId { get; private set; }

        public Task<IReadOnlyList<SocialAccountResponse>> GetAsync(
            CancellationToken cancellationToken) =>
            Task.FromResult(Accounts);

        public Task<SocialConnectResponse> CreateAuthorizationAsync(
            string platform,
            CancellationToken cancellationToken)
        {
            LastPlatform = platform;

            return Task.FromResult(ConnectResult ?? new SocialConnectResponse());
        }

        public Task<SocialAccountResponse?> CompleteAsync(
            string platform,
            string code,
            string state,
            CancellationToken cancellationToken)
        {
            LastCompletedCode = code;
            LastCompletedState = state;

            return Task.FromResult(CompleteResult);
        }

        public Task<SocialAccountResponse?> DisconnectAsync(
            Guid id,
            CancellationToken cancellationToken)
        {
            LastDisconnectedId = id;

            return Task.FromResult(DisconnectResult);
        }
    }
}
