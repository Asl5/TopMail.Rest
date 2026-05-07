using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;
using TopMail.Rest.Controllers;

namespace TopMail.Rest.Swagger;

/// <summary>
/// Adds explicit multipart/form-data request body documentation for MailController.SendMultipart.
/// This is documentation-only and does not change runtime behavior.
/// </summary>
public sealed class SendMultipartRequestBodyOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        if (!IsTargetOperation(context))
            return;

        operation.RequestBody = new OpenApiRequestBody
        {
            Required = true,
            Content =
            {
                ["multipart/form-data"] = new OpenApiMediaType
                {
                    Schema = BuildSchema(),
                    Example = BuildExample()
                }
            }
        };
    }

    private static bool IsTargetOperation(OperationFilterContext context)
    {
        return context.MethodInfo.DeclaringType == typeof(MailController)
               && string.Equals(context.MethodInfo.Name, "SendMultipart", StringComparison.Ordinal);
    }

    private static OpenApiSchema BuildSchema()
    {
        return new OpenApiSchema
        {
            Type = "object",
            Required = new HashSet<string>
            {
                "mittente",
                "destinatario",
                "oggetto",
                "testoMail"
            },
            Properties = new Dictionary<string, OpenApiSchema>(StringComparer.Ordinal)
            {
                ["mittente"] = new OpenApiSchema
                {
                    Type = "string",
                    Description = "Mittente richiesto della mail."
                },
                ["destinatario"] = new OpenApiSchema
                {
                    Type = "string",
                    Description = "Destinatari To separati da ',' o ';'."
                },
                ["cc"] = new OpenApiSchema
                {
                    Type = "string",
                    Description = "Destinatari Cc separati da ',' o ';'."
                },
                ["ccn"] = new OpenApiSchema
                {
                    Type = "string",
                    Description = "Destinatari Ccn/Bcc separati da ',' o ';'."
                },
                ["replyTo"] = new OpenApiSchema
                {
                    Type = "string",
                    Description = "Indirizzo reply-to opzionale."
                },
                ["oggetto"] = new OpenApiSchema
                {
                    Type = "string",
                    Description = "Oggetto della mail."
                },
                ["testoMail"] = new OpenApiSchema
                {
                    Type = "string",
                    Description = "Body mail. Per HTML usare Base64 UTF-8 con typeBody=1."
                },
                ["typeBody"] = new OpenApiSchema
                {
                    Type = "string",
                    Description = "Tipo body: 1/true/html per HTML, altrimenti testo."
                },
                ["inlineCids"] = new OpenApiSchema
                {
                    Type = "string",
                    Description = "CID inline separati da ',' o ';' per i file inline."
                },
                ["files"] = new OpenApiSchema
                {
                    Type = "array",
                    Description = "Allegati standard (ripetere il campo per piu file).",
                    Items = new OpenApiSchema
                    {
                        Type = "string",
                        Format = "binary"
                    }
                },
                ["inlineFiles"] = new OpenApiSchema
                {
                    Type = "array",
                    Description = "Immagini inline (ripetere il campo per piu file).",
                    Items = new OpenApiSchema
                    {
                        Type = "string",
                        Format = "binary"
                    }
                }
            }
        };
    }

    private static IOpenApiAny BuildExample()
    {
        return new OpenApiObject
        {
            ["mittente"] = new OpenApiString("noreply@example.com"),
            ["destinatario"] = new OpenApiString("user@example.com;user2@example.com"),
            ["cc"] = new OpenApiString("audit@example.com"),
            ["ccn"] = new OpenApiString("bcc@example.com"),
            ["replyTo"] = new OpenApiString("support@example.com"),
            ["oggetto"] = new OpenApiString("Report giornaliero"),
            ["testoMail"] = new OpenApiString("PGgxPkNpYW88L2gxPg=="),
            ["typeBody"] = new OpenApiString("1"),
            ["inlineCids"] = new OpenApiString("logo01;footer01")
        };
    }
}
