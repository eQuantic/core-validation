using System.Globalization;

namespace eQuantic.Validation;

/// <summary>
/// Formats user-facing validation messages from stable rule metadata. Shared by the fluent
/// engine and by source-generated validators so localization behaves identically in both.
/// </summary>
public static class ValidationMessages
{
    private static readonly IReadOnlyDictionary<string, object?> EmptyArguments =
        new Dictionary<string, object?>();

    /// <summary>
    /// Resolves the final message for a failed rule, consulting the context's
    /// <see cref="IValidationMessageProvider"/> first and then replacing the
    /// <c>{Property}</c> and argument placeholders.
    /// </summary>
    public static string Format(
        ValidationContext context,
        string path,
        string displayName,
        string code,
        string template,
        IReadOnlyDictionary<string, object?>? arguments = null)
    {
        if (context is null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        var effectiveArguments = arguments ?? EmptyArguments;

        // The descriptor (and its defensive copy of the arguments) is only materialized when a
        // message provider is actually registered — the unlocalized failure path stays lean.
        var resolved = template;
        if (context.MessageProvider is { } messageProvider)
        {
            var descriptor = new ValidationMessageDescriptor(path, displayName, code, template, effectiveArguments);
            resolved = messageProvider.Resolve(descriptor) ?? template;
        }

        var message = resolved.Replace("{Property}", displayName);
        foreach (var argument in effectiveArguments)
        {
            message = message.Replace(
                "{" + argument.Key + "}",
                Convert.ToString(argument.Value, CultureInfo.CurrentCulture) ?? string.Empty);
        }

        return message;
    }
}
