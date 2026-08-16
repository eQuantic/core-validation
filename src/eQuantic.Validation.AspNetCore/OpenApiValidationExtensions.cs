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
    /// Adds validation metadata extraction to OpenAPI document generation in .NET 10.
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
            if (type is not null && !type.IsPrimitive && type != typeof(string) && schema is OpenApiSchema concreteSchema)
            {
                EnrichSchema(concreteSchema, type);
            }

            return Task.CompletedTask;
        });

        return options;
    }

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

            // Range
            var rangeAttr = propertyInfo?.GetCustomAttribute<RangeAttribute>() ??
                            paramInfo?.GetCustomAttribute<RangeAttribute>();
            if (rangeAttr is not null)
            {
                if (decimal.TryParse(rangeAttr.Minimum?.ToString(), out var min))
                {
                    concretePropSchema.Minimum = min.ToString();
                }
                if (decimal.TryParse(rangeAttr.Maximum?.ToString(), out var max))
                {
                    concretePropSchema.Maximum = max.ToString();
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
