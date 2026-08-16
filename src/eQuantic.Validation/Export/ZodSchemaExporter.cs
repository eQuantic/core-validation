using System.Globalization;
using System.Text;

namespace eQuantic.Validation.Export;

/// <summary>Options controlling TypeScript/Zod schema generation.</summary>
public sealed class ZodExportOptions
{
    /// <summary>Gets or sets the exported const name; defaults to the camel-cased model name plus <c>Schema</c>.</summary>
    public string? SchemaName { get; set; }

    /// <summary>Gets or sets whether property names are camel-cased to match System.Text.Json defaults.</summary>
    public bool UseCamelCase { get; set; } = true;

    /// <summary>
    /// Gets or sets the scenario to export. Rules without scenarios are always included;
    /// scenario-scoped rules are included only when this matches one of theirs.
    /// </summary>
    public string? Scenario { get; set; }
}

/// <summary>
/// Exports a validator's rule manifest as a TypeScript Zod schema, so client-side validation is
/// derived from the single C# source of truth. Structural rules (required, length, range,
/// pattern, email) are mapped; opaque predicates are surfaced as comments with their error codes.
/// </summary>
public static class ZodSchemaExporter
{
    /// <summary>Exports the manifest of <paramref name="validator"/> as a Zod schema module.</summary>
    public static string Export(IDescribableValidator validator, ZodExportOptions? options = null)
    {
        if (validator is null)
        {
            throw new ArgumentNullException(nameof(validator));
        }

        return Export(validator.Describe(), options);
    }

    /// <summary>Exports a rule manifest as a Zod schema module.</summary>
    public static string Export(ValidatorDescription description, ZodExportOptions? options = null)
    {
        if (description is null)
        {
            throw new ArgumentNullException(nameof(description));
        }

        options ??= new ZodExportOptions();
        var root = BuildTree(description, options);
        var modelName = description.ModelType.Name;
        var schemaName = options.SchemaName ?? ToCamelCase(modelName) + "Schema";

        var sb = new StringBuilder();
        sb.Append("// Generated from ").Append(modelName).AppendLine(" rules by eQuantic.Validation — do not edit.");
        sb.AppendLine("import { z } from \"zod\";");
        sb.AppendLine();
        sb.Append("export const ").Append(schemaName).Append(" = ");
        WriteObject(sb, root, options, indentLevel: 0);
        sb.AppendLine(";");
        sb.AppendLine();
        sb.Append("export type ").Append(modelName).Append(" = z.infer<typeof ").Append(schemaName).AppendLine(">;");
        return sb.ToString();
    }

    private sealed class Node
    {
        public Dictionary<string, Node> Children { get; } = new(StringComparer.Ordinal);
        public List<ValidationRuleDescriptor> Rules { get; } = new();
        public bool IsArray { get; set; }
    }

    private static Node BuildTree(ValidatorDescription description, ZodExportOptions options)
    {
        var root = new Node();
        foreach (var rule in description.Rules)
        {
            if (rule.Severity != ValidationSeverity.Error || rule.Path.Length == 0)
            {
                continue;
            }

            if (rule.Scenarios.Count > 0 &&
                (options.Scenario is null ||
                 !rule.Scenarios.Contains(options.Scenario, StringComparer.OrdinalIgnoreCase)))
            {
                continue;
            }

            var node = root;
            foreach (var rawSegment in rule.Path.Split('.'))
            {
                var isArray = rawSegment.EndsWith("[]", StringComparison.Ordinal);
                var segment = isArray ? rawSegment.Substring(0, rawSegment.Length - 2) : rawSegment;
                if (!node.Children.TryGetValue(segment, out var child))
                {
                    child = new Node();
                    node.Children.Add(segment, child);
                }

                child.IsArray |= isArray;
                node = child;
            }

            node.Rules.Add(rule);
        }

        return root;
    }

    private static void WriteObject(StringBuilder sb, Node node, ZodExportOptions options, int indentLevel)
    {
        var indent = new string(' ', indentLevel * 2);
        var inner = new string(' ', (indentLevel + 1) * 2);

        sb.AppendLine("z.object({");
        foreach (var entry in node.Children)
        {
            var name = options.UseCamelCase ? ToCamelCase(entry.Key) : entry.Key;
            sb.Append(inner).Append(name).Append(": ");
            WriteValue(sb, entry.Value, options, indentLevel + 1);
            sb.AppendLine(",");
        }

        sb.Append(indent).Append("})");
    }

    private static void WriteValue(StringBuilder sb, Node node, ZodExportOptions options, int indentLevel)
    {
        if (node.IsArray)
        {
            sb.Append("z.array(");
            WriteElement(sb, node, options, indentLevel);
            sb.Append(')');
            return;
        }

        WriteElement(sb, node, options, indentLevel);

        // A member is optional unless a presence rule targets it or it is a non-nullable value
        // type (which always carries a value in JSON and on the C# binder).
        var valueType = node.Rules.Select(static rule => rule.ValueType).FirstOrDefault(static type => type is not null);
        var isNonNullableValueType = valueType is { IsValueType: true } && Nullable.GetUnderlyingType(valueType) is null;
        var hasPresenceRule = node.Rules.Any(static rule =>
            rule.Kind is ValidationRuleKinds.Required
                or ValidationRuleKinds.NotEmpty
                or ValidationRuleKinds.NotWhiteSpace);

        if (!hasPresenceRule && !isNonNullableValueType)
        {
            sb.Append(".optional()");
        }

        AppendOpaqueRuleComment(sb, node);
    }

    private static void WriteElement(StringBuilder sb, Node node, ZodExportOptions options, int indentLevel)
    {
        if (node.Children.Count > 0)
        {
            WriteObject(sb, node, options, indentLevel);
            return;
        }

        sb.Append(BaseExpression(node));
        foreach (var rule in node.Rules)
        {
            sb.Append(Constraint(rule));
        }
    }

    private static string BaseExpression(Node node)
    {
        var valueType = node.Rules.Select(static rule => rule.ValueType).FirstOrDefault(static type => type is not null);
        var underlying = valueType is null ? null : Nullable.GetUnderlyingType(valueType) ?? valueType;

        if (underlying == typeof(string))
        {
            return "z.string()";
        }

        if (underlying == typeof(bool))
        {
            return "z.boolean()";
        }

        if (underlying == typeof(Guid))
        {
            return "z.string().uuid()";
        }

        if (underlying == typeof(int) || underlying == typeof(long) || underlying == typeof(short) ||
            underlying == typeof(byte) || underlying == typeof(uint) || underlying == typeof(ulong) ||
            underlying == typeof(ushort) || underlying == typeof(sbyte))
        {
            return "z.number().int()";
        }

        if (underlying == typeof(double) || underlying == typeof(float) || underlying == typeof(decimal))
        {
            return "z.number()";
        }

        if (underlying == typeof(DateTime) || underlying == typeof(DateTimeOffset))
        {
            return "z.string().datetime()";
        }

        // Presence-only rules on complex members, or unknown types.
        return node.Rules.Any(static rule => rule.Kind is ValidationRuleKinds.MinimumLength
                or ValidationRuleKinds.MaximumLength
                or ValidationRuleKinds.Length
                or ValidationRuleKinds.Pattern
                or ValidationRuleKinds.Email
                or ValidationRuleKinds.NotWhiteSpace)
            ? "z.string()"
            : "z.unknown()";
    }

    private static string Constraint(ValidationRuleDescriptor rule)
    {
        switch (rule.Kind)
        {
            case ValidationRuleKinds.NotEmpty when IsStringRule(rule):
            case ValidationRuleKinds.NotWhiteSpace:
                return ".min(1)";
            case ValidationRuleKinds.Email:
                return ".email()";
            case ValidationRuleKinds.MinimumLength:
                return ".min(" + Number(rule, "MinimumLength") + ")";
            case ValidationRuleKinds.MaximumLength:
                return ".max(" + Number(rule, "MaximumLength") + ")";
            case ValidationRuleKinds.Length:
                return ".min(" + Number(rule, "MinimumLength") + ").max(" + Number(rule, "MaximumLength") + ")";
            case ValidationRuleKinds.Pattern:
                var pattern = rule.Arguments.TryGetValue("Pattern", out var raw) ? raw?.ToString() : null;
                return string.IsNullOrEmpty(pattern)
                    ? string.Empty
                    : ".regex(/" + pattern!.Replace("/", "\\/") + "/)";
            case ValidationRuleKinds.GreaterThan when TryNumber(rule, "Minimum") is { } exclusiveMinimum:
                return ".gt(" + exclusiveMinimum + ")";
            case ValidationRuleKinds.GreaterThanOrEqual when TryNumber(rule, "Minimum") is { } minimum:
                return ".gte(" + minimum + ")";
            case ValidationRuleKinds.LessThan when TryNumber(rule, "Maximum") is { } exclusiveMaximum:
                return ".lt(" + exclusiveMaximum + ")";
            case ValidationRuleKinds.LessThanOrEqual when TryNumber(rule, "Maximum") is { } maximum:
                return ".lte(" + maximum + ")";
            case ValidationRuleKinds.InclusiveBetween when TryNumber(rule, "Minimum") is { } betweenMinimum:
                return ".gte(" + betweenMinimum + ").lte(" + Number(rule, "Maximum") + ")";
            default:
                return string.Empty;
        }
    }

    private static void AppendOpaqueRuleComment(StringBuilder sb, Node node)
    {
        var opaque = node.Rules
            .Where(static rule => rule.Kind is ValidationRuleKinds.Predicate
                or ValidationRuleKinds.Match
                or ValidationRuleKinds.Create
                or ValidationRuleKinds.Parse
                or ValidationRuleKinds.NotEqual
                or ValidationRuleKinds.OneOf ||
                rule.Arguments.ContainsKey("OtherPath"))
            .Select(static rule => rule.Code)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        if (opaque.Length > 0)
        {
            sb.Append(" /* server-side: ").Append(string.Join(", ", opaque)).Append(" */");
        }
    }

    private static bool IsStringRule(ValidationRuleDescriptor rule)
    {
        var underlying = rule.ValueType is null
            ? null
            : Nullable.GetUnderlyingType(rule.ValueType) ?? rule.ValueType;
        return underlying == typeof(string);
    }

    private static string Number(ValidationRuleDescriptor rule, string name) =>
        TryNumber(rule, name) ?? "0";

    private static string? TryNumber(ValidationRuleDescriptor rule, string name) =>
        rule.Arguments.TryGetValue(name, out var value) && value is not null
            ? Convert.ToString(value, CultureInfo.InvariantCulture)
            : null;

    private static string ToCamelCase(string name) =>
        name.Length == 0 || char.IsLower(name[0])
            ? name
            : char.ToLowerInvariant(name[0]) + name.Substring(1);
}
