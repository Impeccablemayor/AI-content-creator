using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Api.Configuration.Swagger;

public sealed class AuthorizeOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        var actionAttributes = context.MethodInfo.GetCustomAttributes(true);
        var controllerAttributes = context.MethodInfo.DeclaringType?
            .GetCustomAttributes(true) ?? [];
        var endpointMetadata = context.ApiDescription.ActionDescriptor.EndpointMetadata;

        if (endpointMetadata.OfType<IAllowAnonymous>().Any() ||
            actionAttributes.OfType<IAllowAnonymous>().Any() ||
            controllerAttributes.OfType<IAllowAnonymous>().Any())
        {
            return;
        }

        var requiresAuthorization = endpointMetadata.OfType<IAuthorizeData>().Any() ||
            actionAttributes.OfType<IAuthorizeData>().Any() ||
            controllerAttributes.OfType<IAuthorizeData>().Any();
        if (!requiresAuthorization)
        {
            return;
        }

        operation.Security ??= [];
        operation.Security.Add(new OpenApiSecurityRequirement
        {
            [new OpenApiSecuritySchemeReference("Bearer", context.Document)] = []
        });
    }
}