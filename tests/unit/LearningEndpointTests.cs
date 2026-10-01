using System.Reflection;
using Api.Application.Learning;
using Api.Contracts.Learning;
using Api.Contracts.Responses;
using Api.Controllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace Api.Tests;

public sealed class LearningEndpointTests
{
    [Fact]
    public void Learning_controller_requires_authentication()
    {
        var controller = typeof(LearningController);

        Assert.NotNull(controller.GetCustomAttribute<AuthorizeAttribute>());
        Assert.Equal("api/v1/learning", controller.GetCustomAttribute<RouteAttribute>()?.Template);
    }

    [Fact]
    public void Insights_can_be_read_and_run_on_the_same_route()
    {
        var read = typeof(LearningController).GetMethod(nameof(LearningController.GetInsights))!;
        var run = typeof(LearningController).GetMethod(nameof(LearningController.GenerateInsights))!;

        Assert.Equal("insights", read.GetCustomAttribute<HttpGetAttribute>()?.Template);
        Assert.Equal("insights", run.GetCustomAttribute<HttpPostAttribute>()?.Template);

        // Two verbs on one path is the whole point: the read is the documented
        // surface, and the run is what fills it.
        Assert.Null(read.GetCustomAttribute<HttpPostAttribute>());
        Assert.Null(run.GetCustomAttribute<HttpGetAttribute>());
    }

    [Fact]
    public void No_learning_route_can_delete_or_overwrite_a_signal()
    {
        var methods = typeof(LearningController)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(method => method.GetCustomAttribute<HttpDeleteAttribute>() is not null
                || method.GetCustomAttribute<HttpPutAttribute>() is not null
                || method.GetCustomAttribute<HttpPatchAttribute>() is not null)
            .ToList();

        // Signals accumulate; rewriting or removing one would erase a measurement
        // that was actually taken.
        Assert.Empty(methods);
    }

    [Fact]
    public void The_read_takes_an_optional_page_size_and_nothing_else()
    {
        var read = typeof(LearningController).GetMethod(nameof(LearningController.GetInsights))!;

        Assert.Equal(
            [typeof(int?), typeof(CancellationToken)],
            read.GetParameters().Select(parameter => parameter.ParameterType));
    }

    [Fact]
    public void The_run_takes_no_body()
    {
        var run = typeof(LearningController).GetMethod(nameof(LearningController.GenerateInsights))!;

        // The window is the configured lookback, not something a caller can ask
        // to be narrowed into a smaller, more flattering sample.
        Assert.Equal([typeof(CancellationToken)], run.GetParameters().Select(parameter => parameter.ParameterType));
        Assert.Null(run.GetCustomAttribute<FromBodyAttribute>());
    }

    [Fact]
    public async Task Read_returns_the_shared_envelope_with_a_list_payload()
    {
        var result = await new LearningController(StubLearning.WithInsights())
            .GetInsights(null, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var envelope = Assert.IsType<ApiResponse<List<LearningInsightResponse>>>(ok.Value);

        var insight = Assert.Single(envelope.Data);
        Assert.Equal("PLATFORM_ENGAGEMENT", insight.Type);
        Assert.Equal("Learning insights retrieved successfully.", envelope.Message);
    }

    [Fact]
    public async Task Read_passes_the_requested_page_size_to_the_service()
    {
        var stub = StubLearning.WithInsights();

        await new LearningController(stub).GetInsights(10, CancellationToken.None);

        Assert.Equal(10, stub.LastLimit);
    }

    [Fact]
    public async Task Read_falls_back_to_the_cap_when_no_page_size_is_given()
    {
        var stub = StubLearning.WithInsights();

        await new LearningController(stub).GetInsights(null, CancellationToken.None);

        Assert.Equal(LearningService.MaxInsightsLimit, stub.LastLimit);
    }

    [Fact]
    public async Task A_run_with_findings_returns_200_and_says_it_had_enough_data()
    {
        var result = await new LearningController(StubLearning.WithRun(hasSufficientData: true))
            .GenerateInsights(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var envelope = Assert.IsType<ApiResponse<LearningInsightsRunResponse>>(ok.Value);

        Assert.True(envelope.Data.HasSufficientData);
        Assert.Single(envelope.Data.Insights);
        Assert.Equal("Learning insights generated successfully.", envelope.Message);
    }

    /// <summary>
    /// A workspace that has not measured enough has nothing to say. That is a
    /// completed run, so it is a 200 with a stated reason rather than an error
    /// the caller has to interpret.
    /// </summary>
    [Fact]
    public async Task A_run_without_enough_data_returns_200_and_explains_why()
    {
        var result = await new LearningController(StubLearning.WithRun(hasSufficientData: false))
            .GenerateInsights(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var envelope = Assert.IsType<ApiResponse<LearningInsightsRunResponse>>(ok.Value);

        Assert.False(envelope.Data.HasSufficientData);
        Assert.Empty(envelope.Data.Insights);
        Assert.Equal(5, envelope.Data.MinimumMeasuredPosts);
        Assert.Equal(2, envelope.Data.MeasuredPosts);
        Assert.Contains("Not enough measured performance", envelope.Message);
    }

    private sealed class StubLearning : ILearningService
    {
        private readonly IReadOnlyList<LearningInsightResponse> _insights;
        private readonly LearningInsightsRunResponse? _run;

        public int LastLimit { get; private set; }

        private StubLearning(
            IReadOnlyList<LearningInsightResponse> insights,
            LearningInsightsRunResponse? run)
        {
            _insights = insights;
            _run = run;
        }

        public static StubLearning WithInsights() =>
            new(
                [new LearningInsightResponse
                {
                    Id = Guid.NewGuid(),
                    Type = "PLATFORM_ENGAGEMENT",
                    Dimension = "Platform",
                    Observation = "An observation",
                    Recommendation = "A recommendation",
                    EvidenceCount = 5,
                    Confidence = 50m
                }],
                null);

        public static StubLearning WithRun(bool hasSufficientData) =>
            new(
                [],
                new LearningInsightsRunResponse
                {
                    PostsInWindow = 2,
                    MeasuredPosts = hasSufficientData ? 9 : 2,
                    MinimumMeasuredPosts = 5,
                    HasSufficientData = hasSufficientData,
                    Insights = hasSufficientData
                        ? [new LearningInsightResponse { Type = "PLATFORM_ENGAGEMENT" }]
                        : []
                });

        public Task<LearningInsightsRunResponse> GenerateInsightsAsync(
            CancellationToken cancellationToken = default) =>
            Task.FromResult(_run!);

        public Task<IReadOnlyList<LearningInsightResponse>> GetInsightsAsync(
            int limit,
            CancellationToken cancellationToken = default)
        {
            LastLimit = limit;
            return Task.FromResult(_insights);
        }
    }
}
