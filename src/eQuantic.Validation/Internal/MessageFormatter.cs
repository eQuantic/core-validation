namespace eQuantic.Validation.Internal;

internal static class MessageFormatter
{
    public static string Format(
        ValidationContext context,
        string path,
        string displayName,
        string code,
        string template,
        IReadOnlyDictionary<string, object?> arguments)
    {
        var descriptor = new ValidationMessageDescriptor(path, displayName, code, template, arguments);
        var resolved = context.MessageProvider?.Resolve(descriptor) ?? template;

        var message = resolved.Replace("{Property}", displayName);
        foreach (var argument in arguments)
        {
            message = message.Replace(
                "{" + argument.Key + "}",
                Convert.ToString(argument.Value, System.Globalization.CultureInfo.CurrentCulture) ?? string.Empty);
        }

        return message;
    }
}
