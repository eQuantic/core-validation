using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace eQuantic.Validation.AspNetCore;

internal static class ValidationProblemDetailsFactory
{
    public static IResult MinimalApi(ValidationResult result)
    {
        return Results.ValidationProblem(
            result.ToErrorDictionary(),
            title: "One or more validation errors occurred.",
            statusCode: StatusCodes.Status400BadRequest,
            extensions: new Dictionary<string, object?>
            {
                // JsonNode serializes under source-generated JSON contexts, keeping the
                // response Native AOT-safe; arbitrary CLR objects in extensions would not.
                ["issues"] = ToJsonIssues(result),
            });
    }

    public static ValidationProblemDetails Mvc(ValidationResult result)
    {
        var details = new ValidationProblemDetails(result.ToErrorDictionary())
        {
            Status = StatusCodes.Status400BadRequest,
            Title = "One or more validation errors occurred.",
        };
        details.Extensions["issues"] = ToPlainIssues(result);
        return details;
    }

    private static JsonArray ToJsonIssues(ValidationResult result)
    {
        var issues = new JsonArray();
        foreach (var failure in result.Failures)
        {
            issues.Add(new JsonObject
            {
                ["path"] = failure.Path,
                ["code"] = failure.Code,
                ["message"] = failure.Message,
                ["severity"] = failure.Severity.ToString(),
            });
        }

        return issues;
    }

    private static List<Dictionary<string, string>> ToPlainIssues(ValidationResult result)
    {
        var issues = new List<Dictionary<string, string>>(result.Failures.Count);
        foreach (var failure in result.Failures)
        {
            issues.Add(new Dictionary<string, string>
            {
                ["path"] = failure.Path,
                ["code"] = failure.Code,
                ["message"] = failure.Message,
                ["severity"] = failure.Severity.ToString(),
            });
        }

        return issues;
    }
}
