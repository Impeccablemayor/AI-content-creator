using System.Reflection;
using Api.Application.AuditLogs;
using Api.Contracts.AuditLogs;
using Api.Contracts.Responses;
using Api.Controllers;
using Api.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Xunit;

namespace Api.Tests;

public sealed class AuditLogsEndpointTests
{
    [Fact]
    public void Audit_logs_controller_requires_authentication()
    {
        var controller = typeof(AuditLogsController);

        Assert.NotNull(controller.GetCustomAttribute<AuthorizeAttribute>());
        Assert.Equal("api/v1/audit-logs", controller.GetCustomAttribute<RouteAttribute>()?.Template);
    }

    /// <summary>
    /// The log is only evidence if it cannot be edited. A verb that could add,
    /// change, or remove a row would let the record be rewritten after the
    /// fact, so the controller must expose reads and nothing else.
    /// </summary>
    [Fact]
    public void Audit_logs_exposes_no_write_verb()
    {
        var methods = typeof(AuditLogsController)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);

        Assert.NotEmpty(methods);
        Assert.All(methods, method =>
        {
            Assert.Null(method.GetCustomAttribute<HttpPostAttribute>());
            Assert.Null(method.GetCustomAttribute<HttpPutAttribute>());
            Assert.Null(method.GetCustomAttribute<HttpPatchAttribute>());
            Assert.Null(method.GetCustomAttribute<HttpDeleteAttribute>());
        });
    }

    [Fact]
    public void Collection_is_a_get_with_the_documented_filters()
    {
        var get = typeof(AuditLogsController).GetMethod(nameof(AuditLogsController.Get));

        Assert.NotNull(get);
        Assert.Null(get!.GetCustomAttribute<HttpGetAttribute>()?.Template);

        var parameters = get.GetParameters();
        Assert.Equal(4, parameters.Length);

        // The action filter is the nullable enum, so compare the underlying type.
        Assert.Equal(typeof(AuditAction), Nullable.GetUnderlyingType(parameters[0].ParameterType));
        Assert.Equal(typeof(string), parameters[1].ParameterType);
        Assert.Equal(typeof(Guid?), parameters[2].ParameterType);
        Assert.Equal(typeof(CancellationToken), parameters[3].ParameterType);

        // All three filters come from the query string, not the route.
        Assert.All(parameters.Take(3), parameter =>
        {
            Assert.NotNull(parameter.GetCustomAttribute<FromQueryAttribute>());
            Assert.Null(parameter.GetCustomAttribute<FromRouteAttribute>());
        });
    }

    [Fact]
    public async Task Get_returns_200_with_the_shared_envelope_and_a_list_payload()
    {
        var result = await new AuditLogsController(StubAuditLogs.WithLogs(
            [new AuditLogResponse { Id = Guid.NewGuid(), Action = "ContentApproved" }]))
            .Get(null, null, null, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var envelope = Assert.IsType<ApiResponse<List<AuditLogResponse>>>(ok.Value);

        Assert.Single(envelope.Data);
        Assert.Equal("Audit logs retrieved successfully.", envelope.Message);
    }

    [Fact]
    public async Task Get_returns_an_empty_list_when_there_is_nothing_to_show()
    {
        var result = await new AuditLogsController(StubAuditLogs.WithLogs([]))
            .Get(null, null, null, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var envelope = Assert.IsType<ApiResponse<List<AuditLogResponse>>>(ok.Value);

        Assert.Empty(envelope.Data);
    }

    [Fact]
    public async Task Get_forwards_the_filters_to_the_service()
    {
        var stub = StubAuditLogs.WithLogs([]);
        var controller = new AuditLogsController(stub);
        var resourceId = Guid.NewGuid();

        await controller.Get(
            AuditAction.ScheduleCreated, "ContentPost", resourceId, CancellationToken.None);

        Assert.Equal(AuditAction.ScheduleCreated, stub.LastAction);
        Assert.Equal("ContentPost", stub.LastEntityType);
        Assert.Equal(resourceId, stub.LastEntityId);
    }

    private sealed class StubAuditLogs(IReadOnlyList<AuditLogResponse> logs) : IAuditLogService
    {
        public AuditAction? LastAction { get; private set; }

        public string? LastEntityType { get; private set; }

        public Guid? LastEntityId { get; private set; }

        public static StubAuditLogs WithLogs(IReadOnlyList<AuditLogResponse> logs) => new(logs);

        public Task<IReadOnlyList<AuditLogResponse>> GetAsync(
            AuditAction? action,
            string? entityType,
            Guid? entityId,
            CancellationToken cancellationToken = default)
        {
            LastAction = action;
            LastEntityType = entityType;
            LastEntityId = entityId;
            return Task.FromResult(logs);
        }

        public Task RecordAsync(
            AuditAction action,
            string entityType,
            Guid? entityId = null,
            object? oldValues = null,
            object? newValues = null,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException("The read endpoint must not record anything.");

        public Task RecordAsync(
            Guid workspaceId,
            Guid? userId,
            AuditAction action,
            string entityType,
            Guid? entityId = null,
            object? oldValues = null,
            object? newValues = null,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException("The read endpoint must not record anything.");
    }
}
