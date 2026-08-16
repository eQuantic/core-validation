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
                ["issues"] = result.Failures,
            });
    }

    public static ValidationProblemDetails Mvc(ValidationResult result)
    {
        var details = new ValidationProblemDetails(result.ToErrorDictionary())
        {
            Status = StatusCodes.Status400BadRequest,
            Title = "One or more validation errors occurred.",
        };
        details.Extensions["issues"] = result.Failures;
        return details;
    }
}
