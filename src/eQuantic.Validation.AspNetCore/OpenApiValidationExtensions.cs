#if NET10_0_OR_GREATER || NET9_0_OR_GREATER
using Microsoft.AspNetCore.OpenApi;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi;
using eQuantic.Validation.Attributes;
using System.Reflection;

namespace eQuantic.Validation.AspNetCore;

/// <summary>
/// OpenAPI schema transformer that enriches OpenAPI document schemas with eQuantic.Validation rules.
/// </summary>
public static class OpenApiValidationExtensions
{
    /// <summary>
    /// Adds validation metadata extraction to OpenAPI document generation in .NET 10. Schemas are
    /// enriched from declarative attributes and from every registered validator that implements
    /// <see cref="IDescribableValidator"/> (fluent and source-generated alike).
    /// </summary>
    public static OpenApiOptions AddValidationTransformer(this OpenApiOptions options)
    {
        if (options is null)
        {
            throw new ArgumentNullException(nameof(options));
        }

        options.AddSchemaTransformer((schema, context, cancellationToken) =>
        {
            var type = context.JsonTypeInfo.Type;
            if (type is null || type.IsPrimitive || type == typeof(string) || schema is not OpenApiSchema concreteSchema)
            {
                return Task.CompletedTask;
            }

            EnrichSchema(concreteSchema, type);

            // Validators are scoped; resolve them in a private scope and apply their manifests.
            using var scope = context.ApplicationServices.CreateScope();
            foreach (var registration in scope.ServiceProvider.GetServices<ValidationRegistration>())
            {
                if (registration.ModelType == type &&
                    registration.Resolve(scope.ServiceProvider) is IDescribableValidator describable)
                {
                    concreteSchema.EnrichSchema(describable.Describe());
                }
            }

            return Task.CompletedTask;
        });

        return options;
    }

    /// <summary>
    /// Enriches an OpenAPI schema from a validator's rule manifest. Only unconditional,
    /// scenario-free, blocking rules targeting top-level members are mapped.
    /// </summary>
    public static OpenApiSchema EnrichSchema(this OpenApiSchema schema, ValidatorDescription description)
    {
        if (schema is null)
        {
            throw new ArgumentNullException(nameof(schema));
        }

        if (description is null)
        {
            throw new ArgumentNullException(nameof(description));
        }

        if (schema.Properties is null || schema.Properties.Count == 0)
        {
            return schema;
        }

        foreach (var rule in description.Rules)
        {
            if (rule.IsConditional ||
                rule.Scenarios.Count > 0 ||
                rule.Severity != ValidationSeverity.Error ||
                rule.Path.Length == 0 ||
                rule.Path.Contains('.') ||
                rule.Path.Contains('['))
            {
                continue;
            }

            string? propertyName = null;
            foreach (var key in schema.Properties.Keys)
            {
                if (string.Equals(key, rule.Path, StringComparison.OrdinalIgnoreCase))
                {
                    propertyName = key;
                    break;
                }
            }

            if (propertyName is null || schema.Properties[propertyName] is not OpenApiSchema propertySchema)
            {
                continue;
            }

            switch (rule.Kind)
            {
                case ValidationRuleKinds.Required:
                case ValidationRuleKinds.NotEmpty:
                case ValidationRuleKinds.NotWhiteSpace:
                    schema.Required ??= new HashSet<string>();
                    schema.Required.Add(propertyName);
                    break;

                case ValidationRuleKinds.Email:
                    propertySchema.Format = "email";
                    break;

                case ValidationRuleKinds.MinimumLength:
                    propertySchema.MinLength = GetInt(rule, "MinimumLength");
                    break;

                case ValidationRuleKinds.MaximumLength:
                    propertySchema.MaxLength = GetInt(rule, "MaximumLength");
                    break;

                case ValidationRuleKinds.Length:
                    propertySchema.MinLength = GetInt(rule, "MinimumLength");
                    propertySchema.MaxLength = GetInt(rule, "MaximumLength");
                    break;

                case ValidationRuleKinds.Pattern:
                    if (GetString(rule, "Pattern") is { Length: > 0 } pattern)
                    {
                        propertySchema.Pattern = pattern;
                    }

                    break;

                case ValidationRuleKinds.GreaterThan:
                    propertySchema.ExclusiveMinimum = GetNumber(rule, "Minimum");
                    break;

                case ValidationRuleKinds.LessThan:
                    propertySchema.ExclusiveMaximum = GetNumber(rule, "Maximum");
                    break;

                case ValidationRuleKinds.InclusiveBetween:
                    propertySchema.Minimum = GetNumber(rule, "Minimum");
                    propertySchema.Maximum = GetNumber(rule, "Maximum");
                    break;
            }
        }

        return schema;
    }

    private static int? GetInt(ValidationRuleDescriptor rule, string name) =>
        rule.Arguments.TryGetValue(name, out var value) && value is not null
            ? Convert.ToInt32(value, System.Globalization.CultureInfo.InvariantCulture)
            : null;

    private static string? GetString(ValidationRuleDescriptor rule, string name) =>
        rule.Arguments.TryGetValue(name, out var value) ? value?.ToString() : null;

    private static string? GetNumber(ValidationRuleDescriptor rule, string name) =>
        rule.Arguments.TryGetValue(name, out var value) && value is not null
            ? Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture)
            : null;

    /// <summary>
    /// Enriches an OpenAPI schema instance using metadata from validation attributes defined on <paramref name="type"/>.
    /// </summary>
    public static OpenApiSchema EnrichSchema(this OpenApiSchema schema, Type type)
    {
        if (schema is null)
        {
            throw new ArgumentNullException(nameof(schema));
        }

        if (type is null)
        {
            throw new ArgumentNullException(nameof(type));
        }

        if (schema.Properties is null || schema.Properties.Count == 0)
        {
            return schema;
        }

        var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);
        var constructorParams = type.GetConstructors()
            .OrderByDescending(c => c.GetParameters().Length)
            .FirstOrDefault()
            ?.GetParameters() ?? Array.Empty<ParameterInfo>();

        foreach (var (propName, propSchema) in schema.Properties)
        {
            if (propSchema is not OpenApiSchema concretePropSchema)
            {
                continue;
            }

            var propertyInfo = properties.FirstOrDefault(p => string.Equals(p.Name, propName, StringComparison.OrdinalIgnoreCase));
            var paramInfo = constructorParams.FirstOrDefault(p => string.Equals(p.Name, propName, StringComparison.OrdinalIgnoreCase));

            // Required check
            var isRequired = (propertyInfo?.GetCustomAttribute<RequiredAttribute>() != null) ||
                             (paramInfo?.GetCustomAttribute<RequiredAttribute>() != null) ||
                             (propertyInfo?.GetCustomAttribute<System.ComponentModel.DataAnnotations.RequiredAttribute>() != null) ||
                             (paramInfo?.GetCustomAttribute<System.ComponentModel.DataAnnotations.RequiredAttribute>() != null);

            if (isRequired)
            {
                schema.Required ??= new HashSet<string>();
                if (!schema.Required.Contains(propName))
                {
                    schema.Required.Add(propName);
                }
            }

            // Email check
            var emailAttr = propertyInfo?.GetCustomAttribute<EmailAttribute>() ??
                            paramInfo?.GetCustomAttribute<EmailAttribute>();
            if (emailAttr is not null)
            {
                concretePropSchema.Format = "email";
            }

            // MinLength / MaxLength
            var minLengthAttr = propertyInfo?.GetCustomAttribute<MinLengthAttribute>() ??
                               paramInfo?.GetCustomAttribute<MinLengthAttribute>();
            if (minLengthAttr is not null)
            {
                concretePropSchema.MinLength = minLengthAttr.Length;
            }

            var maxLengthAttr = propertyInfo?.GetCustomAttribute<MaxLengthAttribute>() ??
                               paramInfo?.GetCustomAttribute<MaxLengthAttribute>();
            if (maxLengthAttr is not null)
            {
                concretePropSchema.MaxLength = maxLengthAttr.Length;
            }

            // Length
            var lengthAttr = propertyInfo?.GetCustomAttribute<LengthAttribute>() ??
                             paramInfo?.GetCustomAttribute<LengthAttribute>();
            if (lengthAttr is not null)
            {
                concretePropSchema.MinLength = lengthAttr.MinimumLength;
                concretePropSchema.MaxLength = lengthAttr.MaximumLength;
            }

            // Range (invariant culture: OpenAPI numeric bounds must use '.' regardless of host locale)
            var rangeAttr = propertyInfo?.GetCustomAttribute<RangeAttribute>() ??
                            paramInfo?.GetCustomAttribute<RangeAttribute>();
            if (rangeAttr is not null)
            {
                if (rangeAttr.Minimum is not null)
                {
                    concretePropSchema.Minimum = Convert.ToString(rangeAttr.Minimum, System.Globalization.CultureInfo.InvariantCulture);
                }
                if (rangeAttr.Maximum is not null)
                {
                    concretePropSchema.Maximum = Convert.ToString(rangeAttr.Maximum, System.Globalization.CultureInfo.InvariantCulture);
                }
            }

            // Pattern
            var patternAttr = propertyInfo?.GetCustomAttribute<PatternAttribute>() ??
                              paramInfo?.GetCustomAttribute<PatternAttribute>();
            if (patternAttr is not null)
            {
                concretePropSchema.Pattern = patternAttr.Regex;
            }
        }

        return schema;
    }
}
#endif
