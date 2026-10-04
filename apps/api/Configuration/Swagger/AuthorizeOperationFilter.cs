using System.Reflection;
using Api.Configuration;
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

        // Both credentials, because both really are accepted, and the generated
        // document has to say so.
        //
        // Separate entries in the array rather than one entry holding both
        // schemes: OpenAPI treats entries as alternatives and the schemes inside
        // one entry as a conjunction, so a single entry would have told a client
        // it must send a cookie *and* a bearer header on every request.
        //
        // The cookie is listed first because it is what a browser uses. The bearer
        // header is not deprecated: it is how non-browser clients authenticate, and
        // nothing in the pipeline treats the two differently.
        operation.Security.Add(new OpenApiSecurityRequirement
        {
            [
                new OpenApiSecuritySchemeReference(
                    AuthCookieOptions.OpenApiSecuritySchemeName,
                    context.Document)] = []
        });

        operation.Security.Add(new OpenApiSecurityRequirement
        {
            [new OpenApiSecuritySchemeReference("Bearer", context.Document)] = []
        });
    }
}