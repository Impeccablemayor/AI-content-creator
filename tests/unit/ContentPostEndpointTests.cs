using System.Reflection;
using Api.Application.ContentPosts;
using Api.Contracts.ContentPost;
using Api.Contracts.Responses;
using Api.Controllers;
using Api.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Xunit;

namespace Api.Tests;

public sealed class ContentPostEndpointTests
{
    [Fact]
    public void Generate_post_route_is_post_and_inherits_authorization()
    {
        var controller = typeof(ContentIdeasController);
        var action = controller.GetMethod(nameof(ContentIdeasController.GeneratePost));

        Assert.NotNull(action);
        Assert.NotNull(controller.GetCustomAttribute<AuthorizeAttribute>());
        Assert.Equal("api/v1/content-ideas", controller.GetCustomAttribute<RouteAttribute>()?.Template);
        Assert.Equal("{id:guid}/generate-post", action.GetCustomAttribute<HttpPostAttribute>()?.Template);
    }

    [Fact]
    public void Content_post_controller_is_authorized_and_scoped_to_its_own_route()
    {
        var controller = typeof(ContentPostsController);

        Assert.NotNull(controller.GetCustomAttribute<AuthorizeAttribute>());
        Assert.Equal("api/v1/content-posts", controller.GetCustomAttribute<RouteAttribute>()?.Template);
    }

    [Fact]
    public void List_is_a_plain_get_on_the_collection()
    {
        var list = GetActions()
            .Single(action => action.GetParameters()[0].ParameterType == typeof(ContentPostStatus?));

        Assert.NotNull(list.GetCustomAttribute<HttpGetAttribute>());
        Assert.Null(list.GetCustomAttribute<HttpGetAttribute>()?.Template);
    }

    [Fact]
    public void Update_is_a_patch_on_the_post_id()
    {
        var update = GetActions().Single(action => action.Name == nameof(ContentPostsController.Update));

        Assert.Equal("{id:guid}", update.GetCustomAttribute<HttpPatchAttribute>()?.Template);
        Assert.Equal(typeof(UpdateContentPostRequest), update.GetParameters()[1].ParameterType);
    }

    [Fact]
    public void Get_by_id_uses_a_get_on_the_guarded_id_template()
    {
        var getById = GetActions()
            .Single(candidate => candidate.Name == nameof(ContentPostsController.Get) &&
                candidate.GetParameters()[0].ParameterType == typeof(Guid));

        Assert.Equal("{id:guid}", getById.GetCustomAttribute<HttpGetAttribute>()?.Template);
    }

    [Theory]
    [InlineData(nameof(ContentPostsController.Regenerate), "{id:guid}/regenerate")]
    [InlineData(nameof(ContentPostsController.Approve), "{id:guid}/approve")]
    [InlineData(nameof(ContentPostsController.Reject), "{id:guid}/reject")]
    [InlineData(nameof(ContentPostsController.Evaluate), "{id:guid}/evaluate")]
    [InlineData(nameof(ContentPostsController.Schedule), "{id:guid}/schedule")]
    public void Post_actions_use_post_verbs_on_guarded_id_templates(string actionName, string expectedTemplate)
    {
        var action = GetActions()
            .Single(candidate => candidate.Name == actionName &&
                candidate.GetParameters()[0].ParameterType == typeof(Guid));

        Assert.Equal(expectedTemplate, action.GetCustomAttribute<HttpPostAttribute>()?.Template);
    }

    [Fact]
    public async Task List_returns_the_success_envelope()
    {
        var data = new List<ContentPostResponse> { new() { Id = Guid.NewGuid(), Status = "Draft" } };
        var controller = new ContentPostsController(new StubContentPostService { List = data });

        var result = await controller.Get(null, null, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var envelope = Assert.IsType<ApiResponse<List<ContentPostResponse>>>(ok.Value);
        Assert.Equal("Content posts retrieved successfully.", envelope.Message);
        Assert.Equal(data, envelope.Data);
    }

    [Fact]
    public async Task Get_returns_the_success_envelope()
    {
        var post = new ContentPostResponse { Id = Guid.NewGuid(), Status = "Draft" };
        var controller = new ContentPostsController(new StubContentPostService { Single = post });

        var result = await controller.Get(post.Id, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var envelope = Assert.IsType<ApiResponse<ContentPostResponse>>(ok.Value);
        Assert.Equal("Content post retrieved successfully.", envelope.Message);
        Assert.Same(post, envelope.Data);
    }

    [Theory]
    [InlineData(nameof(ContentPostsController.Get))]
    [InlineData(nameof(ContentPostsController.Update))]
    [InlineData(nameof(ContentPostsController.Regenerate))]
    [InlineData(nameof(ContentPostsController.Approve))]
    [InlineData(nameof(ContentPostsController.Reject))]
    public async Task Missing_post_returns_a_404_with_the_standard_error_envelope(string actionName)
    {
        var controller = new ContentPostsController(new StubContentPostService { Single = null });

        var result = await InvokeAsync(controller, actionName);

        var notFound = Assert.IsType<NotFoundObjectResult>(result);
        var error = Assert.IsType<ApiErrorResponse>(notFound.Value);
        Assert.Equal("CONTENT_POST_NOT_FOUND", error.Error.Code);
        Assert.False(string.IsNullOrWhiteSpace(error.Error.Message));
    }

    [Theory]
    [InlineData(nameof(ContentPostsController.Regenerate))]
    [InlineData(nameof(ContentPostsController.Approve))]
    [InlineData(nameof(ContentPostsController.Reject))]
    public async Task Invalid_transition_returns_a_409_with_the_standard_error_envelope(string actionName)
    {
        var message = "Only draft content posts can be approved.";
        var controller = new ContentPostsController(new StubContentPostService
        {
            Single = new ContentPostResponse { Id = Guid.NewGuid() },
            TransitionFailure = new InvalidContentPostTransitionException(message)
        });

        var result = await InvokeAsync(controller, actionName);

        var conflict = Assert.IsType<ConflictObjectResult>(result);
        var error = Assert.IsType<ApiErrorResponse>(conflict.Value);
        Assert.Equal("INVALID_CONTENT_POST_TRANSITION", error.Error.Code);
        Assert.Equal(message, error.Error.Message);
    }

    [Fact]
    public async Task Invalid_edited_content_returns_a_400_with_the_standard_error_envelope()
    {
        var message = "The content post contained missing or oversized post content.";
        var controller = new ContentPostsController(new StubContentPostService
        {
            Single = new ContentPostResponse { Id = Guid.NewGuid() },
            ContentFailure = new InvalidContentPostContentException(message)
        });

        var result = await controller.Update(
            Guid.NewGuid(),
            CreateRequest(),
            CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        var error = Assert.IsType<ApiErrorResponse>(badRequest.Value);
        Assert.Equal("INVALID_CONTENT_POST_CONTENT", error.Error.Code);
        Assert.Equal(message, error.Error.Message);
    }

    [Fact]
    public async Task Update_returns_the_success_envelope()
    {
        var post = new ContentPostResponse { Id = Guid.NewGuid(), Status = "Draft" };
        var controller = new ContentPostsController(new StubContentPostService { Single = post });

        var result = await controller.Update(Guid.NewGuid(), CreateRequest(), CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var envelope = Assert.IsType<ApiResponse<ContentPostResponse>>(ok.Value);
        Assert.Equal("Content post updated successfully.", envelope.Message);
        Assert.Same(post, envelope.Data);
    }

    [Fact]
    public async Task Schedule_returns_the_success_envelope_with_the_new_time()
    {
        var scheduledAt = DateTime.UtcNow.AddHours(2);
        var post = new ContentPostResponse
        {
            Id = Guid.NewGuid(),
            Status = "Scheduled",
            ScheduledAt = scheduledAt
        };
        var controller = new ContentPostsController(new StubContentPostService
        {
            Scheduled = post
        });

        var result = await controller.Schedule(
            post.Id,
            new ScheduleContentPostRequest { ScheduledAt = scheduledAt },
            CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var envelope = Assert.IsType<ApiResponse<ContentPostResponse>>(ok.Value);
        Assert.Equal("Content post scheduled successfully.", envelope.Message);
        Assert.Equal("Scheduled", envelope.Data.Status);
        Assert.Equal(scheduledAt, envelope.Data.ScheduledAt);
    }

    [Fact]
    public async Task Schedule_returns_404_for_an_unknown_or_foreign_post()
    {
        var controller = new ContentPostsController(new StubContentPostService
        {
            Scheduled = null
        });

        var result = await controller.Schedule(
            Guid.NewGuid(),
            new ScheduleContentPostRequest { ScheduledAt = DateTimeOffset.UtcNow.AddHours(1) },
            CancellationToken.None);

        var notFound = Assert.IsType<NotFoundObjectResult>(result);
        Assert.Equal(
            "CONTENT_POST_NOT_FOUND",
            Assert.IsType<ApiErrorResponse>(notFound.Value).Error.Code);
    }

    [Fact]
    public void Update_contract_still_exposes_no_status_or_schedule_field()
    {
        var properties = typeof(UpdateContentPostRequest)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(property => property.Name)
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.DoesNotContain("Status", properties);
        Assert.DoesNotContain("ScheduledAt", properties);
        Assert.DoesNotContain("PublishClaimedAt", properties);
    }

    [Fact]
    public async Task Evaluate_returns_the_success_envelope_without_changing_status()
    {
        var post = new ContentPostResponse { Id = Guid.NewGuid(), Status = "Draft" };
        var evaluation = new ContentEvaluationResponse
        {
            ContentPostId = post.Id,
            OverallScore = 88m,
            Passed = true,
            Recommendation = "MinorEdit",
            PassThreshold = 70m
        };
        var controller = new ContentPostsController(new StubContentPostService
        {
            Single = post,
            Evaluation = evaluation
        });

        var result = await controller.Evaluate(post.Id, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var envelope = Assert.IsType<ApiResponse<ContentEvaluationResponse>>(ok.Value);
        Assert.Equal("Content post evaluated successfully.", envelope.Message);
        Assert.Same(evaluation, envelope.Data);
        Assert.Equal("Draft", post.Status);
    }

    [Fact]
    public async Task Evaluate_returns_404_for_an_unknown_or_foreign_post()
    {
        var controller = new ContentPostsController(new StubContentPostService
        {
            Single = null,
            Evaluation = null
        });

        var result = await controller.Evaluate(Guid.NewGuid(), CancellationToken.None);

        var notFound = Assert.IsType<NotFoundObjectResult>(result);
        Assert.Equal(
            "CONTENT_POST_NOT_FOUND",
            Assert.IsType<ApiErrorResponse>(notFound.Value).Error.Code);
    }

    [Fact]
    public void Update_contract_exposes_no_system_controlled_field()    {
        var properties = typeof(UpdateContentPostRequest)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(property => property.Name)
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(
            [
                "CallToAction",
                "Content",
                "ContentType",
                "Hashtags",
                "Hook",
                "StorytellingStructure"
            ],
            properties);
    }

    [Fact]
    public void Response_contract_exposes_both_timestamps()
    {
        Assert.NotNull(typeof(ContentPostResponse).GetProperty("CreatedAt"));
        Assert.NotNull(typeof(ContentPostResponse).GetProperty("UpdatedAt"));
    }

    private static IEnumerable<MethodInfo> GetActions() =>
        typeof(ContentPostsController)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(method => method.GetCustomAttributes<HttpMethodAttribute>().Any());

    private static UpdateContentPostRequest CreateRequest() => new()
    {
        Content = "content",
        Hook = "hook",
        CallToAction = string.Empty,
        Hashtags = [],
        ContentType = ContentType.Insight,
        StorytellingStructure = "Hook → Insight"
    };

    private static async Task<IActionResult> InvokeAsync(ContentPostsController controller, string actionName) =>
        actionName switch
        {
            nameof(ContentPostsController.Get) =>
                await controller.Get(Guid.NewGuid(), CancellationToken.None),
            nameof(ContentPostsController.Update) =>
                await controller.Update(Guid.NewGuid(), CreateRequest(), CancellationToken.None),
            nameof(ContentPostsController.Regenerate) =>
                await controller.Regenerate(Guid.NewGuid(), CancellationToken.None),
            nameof(ContentPostsController.Approve) =>
                await controller.Approve(Guid.NewGuid(), CancellationToken.None),
            _ => await controller.Reject(Guid.NewGuid(), CancellationToken.None)
        };

    private sealed class StubContentPostService : IContentPostService
    {
        public IReadOnlyList<ContentPostResponse> List { get; init; } = [];

        public ContentPostResponse? Single { get; init; }

        public InvalidContentPostTransitionException? TransitionFailure { get; init; }

        public InvalidContentPostContentException? ContentFailure { get; init; }

        public ContentEvaluationResponse? Evaluation { get; init; }

        public ContentPostResponse? Scheduled { get; init; }

        public InvalidContentPostScheduleTimeException? ScheduleTimeFailure { get; init; }

        public Task<ContentPostResponse?> ScheduleAsync(
            Guid id,
            ScheduleContentPostRequest request,
            CancellationToken cancellationToken = default) =>
            ScheduleTimeFailure is not null
                ? throw ScheduleTimeFailure
                : Task.FromResult(Scheduled);

        public Task<ContentPostResponse?> GenerateFromIdeaAsync(
            Guid contentIdeaId,
            GenerateContentPostRequest request,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<ContentPostResponse>> GetAsync(
            ContentPostStatus? status,
            ContentPlatform? platform,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(List);

        public Task<ContentPostResponse?> GetAsync(
            Guid id,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(Single);

        public Task<ContentPostResponse?> UpdateAsync(
            Guid id,
            UpdateContentPostRequest request,
            CancellationToken cancellationToken = default) =>
            ContentFailure is not null
                ? throw ContentFailure
                : Task.FromResult(Single);

        public Task<ContentPostResponse?> RegenerateAsync(
            Guid id,
            CancellationToken cancellationToken = default) =>
            TransitionFailure is not null
                ? throw TransitionFailure
                : Task.FromResult(Single);

        public Task<ContentPostResponse?> ApproveAsync(
            Guid id,
            CancellationToken cancellationToken = default) =>
            TransitionFailure is not null
                ? throw TransitionFailure
                : Task.FromResult(Single);

        public Task<ContentPostResponse?> RejectAsync(
            Guid id,
            CancellationToken cancellationToken = default) =>
            TransitionFailure is not null
                ? throw TransitionFailure
                : Task.FromResult(Single);

        public Task<ContentEvaluationResponse?> EvaluateAsync(
            Guid id,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(Evaluation);
    }
}
