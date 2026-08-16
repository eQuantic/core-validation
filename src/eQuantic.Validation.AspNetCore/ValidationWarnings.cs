using System.Text;
using Microsoft.AspNetCore.Http;

namespace eQuantic.Validation.AspNetCore;

/// <summary>
/// How valid-but-warned results surface to HTTP callers. Blocking errors produce a 400 problem
/// response; non-blocking warnings ride along with the successful response instead of dying on
/// the server: a compact <c>Validation-Warnings</c> header (<c>path:code</c> pairs) plus the full
/// failures in <see cref="HttpContext.Items"/> for handlers that want to enrich the body.
/// </summary>
public static class ValidationWarnings
{
    /// <summary>Response header listing warning codes for a valid request.</summary>
    public const string HeaderName = "Validation-Warnings";

    /// <summary>Key under which the warning failures are stored in <see cref="HttpContext.Items"/>.</summary>
    public const string ItemsKey = "eQuantic.Validation.Warnings";

    /// <summary>Gets the warnings collected while validating the current request, if any.</summary>
    public static IReadOnlyList<ValidationFailure>? GetWarnings(HttpContext httpContext)
    {
        if (httpContext is null)
        {
            throw new ArgumentNullException(nameof(httpContext));
        }

        return httpContext.Items.TryGetValue(ItemsKey, out var value)
            ? value as IReadOnlyList<ValidationFailure>
            : null;
    }

    internal static void Attach(HttpContext httpContext, List<ValidationFailure> warnings)
    {
        if (warnings.Count == 0)
        {
            return;
        }

        httpContext.Items[ItemsKey] = warnings;
        httpContext.Response.Headers[HeaderName] = FormatHeader(warnings);
    }

    /// <summary>Formats warnings as a compact, ASCII-safe header value: <c>path:code, path:code</c>.</summary>
    public static string FormatHeader(IReadOnlyList<ValidationFailure> warnings)
    {
        if (warnings is null)
        {
            throw new ArgumentNullException(nameof(warnings));
        }

        var sb = new StringBuilder();
        foreach (var warning in warnings)
        {
            if (sb.Length > 0)
            {
                sb.Append(", ");
            }

            if (warning.Path.Length > 0)
            {
                sb.Append(warning.Path).Append(':');
            }

            sb.Append(warning.Code);
        }

        return sb.ToString();
    }
}
