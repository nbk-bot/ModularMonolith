using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace BuildingBlocks.Infrastructure.OpenApi;

/// <summary>
/// Adds a Bearer JWT security scheme to the generated OpenAPI document and
/// applies it as a default requirement on every operation. The endpoint
/// served by <c>MapOpenApi</c> (<c>/openapi/v1.json</c>) therefore advertises
/// <c>bearerAuth</c> (<c>type: http, scheme: bearer, bearerFormat: JWT</c>).
/// </summary>
public sealed class BearerSecuritySchemeTransformer : IOpenApiDocumentTransformer
{
    public Task TransformAsync(OpenApiDocument document, OpenApiDocumentTransformerContext context, CancellationToken cancellationToken)
    {
        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();

        document.Components.SecuritySchemes["bearerAuth"] = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            In = ParameterLocation.Header,
            Description = "Paste your JWT access token (without the 'Bearer ' prefix).",
        };

        var requirement = new OpenApiSecurityRequirement
        {
            [new OpenApiSecuritySchemeReference("bearerAuth", document, null)] = new List<string>(),
        };

        document.Security ??= new List<OpenApiSecurityRequirement>();
        document.Security.Add(requirement);

        return Task.CompletedTask;
    }
}
