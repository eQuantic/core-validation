using System.Collections.Immutable;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace eQuantic.Validation.Generator;

/// <summary>
/// Incremental generator that generates zero-allocation validators and explicit DI registrations.
/// </summary>
[Generator(LanguageNames.CSharp)]
public sealed class ValidatorRegistrationGenerator : IIncrementalGenerator
{
    private const string ValidatorNamespace = "eQuantic.Validation";
    private const string ValidatorInterfaceName = "IValidator`1";
    private const string GenerateValidatorAttributeName = "eQuantic.Validation.Attributes.GenerateValidatorAttribute";
    private const string AspNetCoreExtensionsType = "eQuantic.Validation.AspNetCore.ServiceCollectionExtensions";
    private const string ServiceCollectionType = "Microsoft.Extensions.DependencyInjection.IServiceCollection";

    /// <inheritdoc />
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // 1. Discover classes implementing IValidator<T> directly (manual validators)
        var manualValidators = context.SyntaxProvider
            .CreateSyntaxProvider(
                static (node, _) => node is ClassDeclarationSyntax { BaseList: not null },
                static (syntaxContext, _) => CreateManualValidatorDescriptor(syntaxContext))
            .Where(static descriptor => descriptor is not null)
            .Select(static (descriptor, _) => descriptor!);

        // 2. Discover classes/records annotated with [GenerateValidator]
        var modelsToGenerate = context.SyntaxProvider
            .CreateSyntaxProvider(
                static (node, _) => node is TypeDeclarationSyntax { AttributeLists.Count: > 0 },
                static (syntaxContext, _) => CreateModelDescriptor(syntaxContext))
            .Where(static descriptor => descriptor is not null)
            .Select(static (descriptor, _) => descriptor!);

        // 3. Generate validation code for models annotated with [GenerateValidator]
        context.RegisterSourceOutput(modelsToGenerate, static (productionContext, model) =>
        {
            GenerateModelValidator(productionContext, model);
        });

        // 4. Combine manual validators + generated validators to emit DI extensions
        var allDiscovered = manualValidators.Collect().Combine(modelsToGenerate.Collect());

        context.RegisterSourceOutput(
            context.CompilationProvider.Combine(allDiscovered),
            static (productionContext, source) =>
            {
                var compilation = source.Left;
                var (manuals, generatedModels) = source.Right;
                GenerateRegistrations(productionContext, compilation, manuals, generatedModels);
            });
    }

    private static ValidatorDescriptor? CreateManualValidatorDescriptor(GeneratorSyntaxContext context)
    {
        return context.SemanticModel.GetDeclaredSymbol(context.Node) is INamedTypeSymbol validatorType
            ? CreateManualValidatorDescriptor(validatorType, requirePublic: false)
            : null;
    }

    private static ValidatorDescriptor? CreateManualValidatorDescriptor(INamedTypeSymbol validatorType, bool requirePublic)
    {
        if (validatorType.IsAbstract ||
            validatorType.IsGenericType ||
            !IsAccessible(validatorType, requirePublic))
        {
            return null;
        }

        var validatorInterface = validatorType.AllInterfaces.FirstOrDefault(static type =>
            type.OriginalDefinition.MetadataName == ValidatorInterfaceName &&
            type.OriginalDefinition.ContainingNamespace.ToDisplayString() == ValidatorNamespace);

        if (validatorInterface is null ||
            validatorInterface.TypeArguments[0] is not INamedTypeSymbol modelType ||
            modelType.TypeKind == TypeKind.Error ||
            !IsAccessible(modelType, requirePublic))
        {
            return null;
        }

        return new ValidatorDescriptor(
            validatorType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
            modelType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat));
    }

    private static ModelValidationDescriptor? CreateModelDescriptor(GeneratorSyntaxContext context)
    {
        if (context.SemanticModel.GetDeclaredSymbol(context.Node) is not INamedTypeSymbol typeSymbol)
        {
            return null;
        }

        var hasGenerateAttribute = typeSymbol.GetAttributes().Any(static attr =>
            attr.AttributeClass?.ToDisplayString() == GenerateValidatorAttributeName);

        if (!hasGenerateAttribute)
        {
            return null;
        }

        var properties = new List<PropertyValidationDescriptor>();
        foreach (var member in typeSymbol.GetMembers())
        {
            if (member is not IPropertySymbol property ||
                property.IsStatic ||
                property.DeclaredAccessibility != Accessibility.Public ||
                property.GetMethod is null)
            {
                continue;
            }

            var rules = ExtractPropertyRules(property, typeSymbol);
            if (rules.Count > 0)
            {
                properties.Add(new PropertyValidationDescriptor(
                    property.Name,
                    property.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                    property.Type.IsReferenceType || property.Type.NullableAnnotation == NullableAnnotation.Annotated,
                    rules));
            }
        }

        var namespaceName = typeSymbol.ContainingNamespace.IsGlobalNamespace
            ? string.Empty
            : typeSymbol.ContainingNamespace.ToDisplayString();

        var generatedValidatorName = typeSymbol.Name + "GeneratedValidator";
        var fullGeneratedValidatorName = string.IsNullOrEmpty(namespaceName)
            ? "global::" + generatedValidatorName
            : "global::" + namespaceName + "." + generatedValidatorName;

        return new ModelValidationDescriptor(
            typeSymbol.Name,
            namespaceName,
            typeSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
            generatedValidatorName,
            fullGeneratedValidatorName,
            properties);
    }

    private static List<RuleDescriptor> ExtractPropertyRules(IPropertySymbol property, INamedTypeSymbol containingType)
    {
        var allAttributes = new List<AttributeData>();
        allAttributes.AddRange(property.GetAttributes());

        // Check primary constructor parameters for record properties
        foreach (var constructor in containingType.Constructors)
        {
            var param = constructor.Parameters.FirstOrDefault(p =>
                string.Equals(p.Name, property.Name, StringComparison.OrdinalIgnoreCase));

            if (param is not null)
            {
                foreach (var attr in param.GetAttributes())
                {
                    if (!allAttributes.Any(a => a.AttributeClass?.ToDisplayString() == attr.AttributeClass?.ToDisplayString()))
                    {
                        allAttributes.Add(attr);
                    }
                }
            }
        }

        var rules = new List<RuleDescriptor>();
        foreach (var attribute in allAttributes)
        {
            var attrClass = attribute.AttributeClass;
            if (attrClass is null)
            {
                continue;
            }

            var attrFullName = attrClass.ToDisplayString();

            // Extract common metadata: Code, Message, Severity, Scenarios
            string? code = null;
            string? message = null;
            int severity = 0; // 0 = Error, 1 = Warning, 2 = Info
            string[]? scenarios = null;

            foreach (var namedArg in attribute.NamedArguments)
            {
                switch (namedArg.Key)
                {
                    case "Code":
                        code = namedArg.Value.Value?.ToString();
                        break;
                    case "Message":
                        message = namedArg.Value.Value?.ToString();
                        break;
                    case "Severity":
                        if (namedArg.Value.Value is int sevInt) severity = sevInt;
                        break;
                    case "Scenarios":
                        if (!namedArg.Value.IsNull && namedArg.Value.Values.Length > 0)
                        {
                            scenarios = namedArg.Value.Values
                                .Select(v => v.Value?.ToString())
                                .Where(v => !string.IsNullOrEmpty(v))
                                .ToArray()!;
                        }
                        break;
                }
            }

            // Match eQuantic rules & DataAnnotations
            if (attrFullName == "eQuantic.Validation.Attributes.RequiredAttribute" ||
                attrFullName == "System.ComponentModel.DataAnnotations.RequiredAttribute")
            {
                rules.Add(new RuleDescriptor(
                    RuleKind.Required,
                    code ?? "required",
                    message ?? "{Property} is required.",
                    severity,
                    scenarios,
                    ImmutableDictionary<string, object?>.Empty));
            }
            else if (attrFullName == "eQuantic.Validation.Attributes.NotEmptyAttribute")
            {
                rules.Add(new RuleDescriptor(
                    RuleKind.NotEmpty,
                    code ?? "not_empty",
                    message ?? "{Property} must not be empty.",
                    severity,
                    scenarios,
                    ImmutableDictionary<string, object?>.Empty));
            }
            else if (attrFullName == "eQuantic.Validation.Attributes.NotWhiteSpaceAttribute")
            {
                rules.Add(new RuleDescriptor(
                    RuleKind.NotWhiteSpace,
                    code ?? "not_whitespace",
                    message ?? "{Property} must not be blank.",
                    severity,
                    scenarios,
                    ImmutableDictionary<string, object?>.Empty));
            }
            else if (attrFullName == "eQuantic.Validation.Attributes.EmailAttribute" ||
                     attrFullName == "System.ComponentModel.DataAnnotations.EmailAddressAttribute")
            {
                rules.Add(new RuleDescriptor(
                    RuleKind.Email,
                    code ?? "email",
                    message ?? "{Property} must be a valid email address.",
                    severity,
                    scenarios,
                    ImmutableDictionary<string, object?>.Empty));
            }
            else if (attrFullName == "eQuantic.Validation.Attributes.MinLengthAttribute" ||
                     attrFullName == "System.ComponentModel.DataAnnotations.MinLengthAttribute")
            {
                var length = attribute.ConstructorArguments.Length > 0 && attribute.ConstructorArguments[0].Value is int len ? len : 0;
                rules.Add(new RuleDescriptor(
                    RuleKind.MinLength,
                    code ?? "minimum_length",
                    message ?? "{Property} must contain at least {MinimumLength} characters.",
                    severity,
                    scenarios,
                    new Dictionary<string, object?> { ["MinimumLength"] = length }.ToImmutableDictionary()));
            }
            else if (attrFullName == "eQuantic.Validation.Attributes.MaxLengthAttribute" ||
                     attrFullName == "System.ComponentModel.DataAnnotations.MaxLengthAttribute")
            {
                var length = attribute.ConstructorArguments.Length > 0 && attribute.ConstructorArguments[0].Value is int len ? len : 0;
                rules.Add(new RuleDescriptor(
                    RuleKind.MaxLength,
                    code ?? "maximum_length",
                    message ?? "{Property} must contain no more than {MaximumLength} characters.",
                    severity,
                    scenarios,
                    new Dictionary<string, object?> { ["MaximumLength"] = length }.ToImmutableDictionary()));
            }
            else if (attrFullName == "eQuantic.Validation.Attributes.LengthAttribute" ||
                     attrFullName == "System.ComponentModel.DataAnnotations.StringLengthAttribute")
            {
                int min = 0;
                int max = 0;
                if (attribute.ConstructorArguments.Length >= 2)
                {
                    min = attribute.ConstructorArguments[0].Value is int m ? m : 0;
                    max = attribute.ConstructorArguments[1].Value is int mx ? mx : 0;
                }
                else if (attribute.ConstructorArguments.Length == 1)
                {
                    max = attribute.ConstructorArguments[0].Value is int mx ? mx : 0;
                    foreach (var namedArg in attribute.NamedArguments)
                    {
                        if (namedArg.Key == "MinimumLength" && namedArg.Value.Value is int mn) min = mn;
                    }
                }

                rules.Add(new RuleDescriptor(
                    RuleKind.Length,
                    code ?? "range",
                    message ?? "{Property} must contain between {MinimumLength} and {MaximumLength} characters.",
                    severity,
                    scenarios,
                    new Dictionary<string, object?> { ["MinimumLength"] = min, ["MaximumLength"] = max }.ToImmutableDictionary()));
            }
            else if (attrFullName == "eQuantic.Validation.Attributes.RangeAttribute" ||
                     attrFullName == "System.ComponentModel.DataAnnotations.RangeAttribute")
            {
                object? min = attribute.ConstructorArguments.Length > 0 ? attribute.ConstructorArguments[0].Value : null;
                object? max = attribute.ConstructorArguments.Length > 1 ? attribute.ConstructorArguments[1].Value : null;
                rules.Add(new RuleDescriptor(
                    RuleKind.Range,
                    code ?? "range",
                    message ?? "{Property} must be between {Minimum} and {Maximum}.",
                    severity,
                    scenarios,
                    new Dictionary<string, object?> { ["Minimum"] = min, ["Maximum"] = max }.ToImmutableDictionary()));
            }
            else if (attrFullName == "eQuantic.Validation.Attributes.PatternAttribute" ||
                     attrFullName == "System.ComponentModel.DataAnnotations.RegularExpressionAttribute")
            {
                var pattern = attribute.ConstructorArguments.Length > 0 ? attribute.ConstructorArguments[0].Value?.ToString() ?? "" : "";
                rules.Add(new RuleDescriptor(
                    RuleKind.Pattern,
                    code ?? "pattern",
                    message ?? "{Property} has an invalid format.",
                    severity,
                    scenarios,
                    new Dictionary<string, object?> { ["Pattern"] = pattern }.ToImmutableDictionary()));
            }
            else if (attrFullName == "eQuantic.Validation.Attributes.GreaterThanAttribute")
            {
                object? val = attribute.ConstructorArguments.Length > 0 ? attribute.ConstructorArguments[0].Value : null;
                rules.Add(new RuleDescriptor(
                    RuleKind.GreaterThan,
                    code ?? "range",
                    message ?? "{Property} must be greater than {Minimum}.",
                    severity,
                    scenarios,
                    new Dictionary<string, object?> { ["Minimum"] = val }.ToImmutableDictionary()));
            }
            else if (attrFullName == "eQuantic.Validation.Attributes.LessThanAttribute")
            {
                object? val = attribute.ConstructorArguments.Length > 0 ? attribute.ConstructorArguments[0].Value : null;
                rules.Add(new RuleDescriptor(
                    RuleKind.LessThan,
                    code ?? "range",
                    message ?? "{Property} must be less than {Maximum}.",
                    severity,
                    scenarios,
                    new Dictionary<string, object?> { ["Maximum"] = val }.ToImmutableDictionary()));
            }
            else if (attrFullName == "eQuantic.Validation.Attributes.ValidateNestedAttribute")
            {
                rules.Add(new RuleDescriptor(
                    RuleKind.ValidateNested,
                    code ?? "nested",
                    message ?? "{Property} is invalid.",
                    severity,
                    scenarios,
                    ImmutableDictionary<string, object?>.Empty));
            }
            else if (attrFullName == "eQuantic.Validation.Attributes.ValidateEachAttribute")
            {
                rules.Add(new RuleDescriptor(
                    RuleKind.ValidateEach,
                    code ?? "collection",
                    message ?? "{Property} is invalid.",
                    severity,
                    scenarios,
                    ImmutableDictionary<string, object?>.Empty));
            }
            else if (attrFullName == "eQuantic.Validation.Attributes.CustomRuleAttribute")
            {
                string methodName = "";
                string? declaringType = null;
                if (attribute.ConstructorArguments.Length == 1)
                {
                    methodName = attribute.ConstructorArguments[0].Value?.ToString() ?? "";
                }
                else if (attribute.ConstructorArguments.Length == 2)
                {
                    if (attribute.ConstructorArguments[0].Value is ITypeSymbol typeSym)
                    {
                        declaringType = typeSym.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                    }
                    methodName = attribute.ConstructorArguments[1].Value?.ToString() ?? "";
                }

                rules.Add(new RuleDescriptor(
                    RuleKind.CustomRule,
                    code ?? "predicate",
                    message ?? "{Property} is invalid.",
                    severity,
                    scenarios,
                    new Dictionary<string, object?> { ["MethodName"] = methodName, ["DeclaringType"] = declaringType }.ToImmutableDictionary()));
            }
        }

        return rules;
    }

    private static void GenerateModelValidator(SourceProductionContext context, ModelValidationDescriptor model)
    {
        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated />");
        sb.AppendLine("#nullable enable");
        sb.AppendLine();

        if (!string.IsNullOrEmpty(model.Namespace))
        {
            sb.Append("namespace ").AppendLine(model.Namespace);
            sb.AppendLine("{");
        }

        var indent = string.IsNullOrEmpty(model.Namespace) ? "" : "    ";

        sb.Append(indent).Append("/// <summary>Generated zero-allocation validator for <see cref=\"").Append(model.ModelName).AppendLine("\"/>.</summary>");
        sb.Append(indent).Append("[global::System.CodeDom.Compiler.GeneratedCode(\"eQuantic.Validation.Generator\", \"1.0.0\")]").AppendLine();
        sb.Append(indent).Append("public sealed class ").Append(model.GeneratedValidatorName)
            .Append(" : global::eQuantic.Validation.IValidator<").Append(model.FullModelTypeName).AppendLine(">");
        sb.Append(indent).AppendLine("{");

        var innerIndent = indent + "    ";

        // ValidatedType property
        sb.Append(innerIndent).AppendLine("/// <inheritdoc />");
        sb.Append(innerIndent).Append("public global::System.Type ValidatedType => typeof(").Append(model.FullModelTypeName).AppendLine(");");
        sb.AppendLine();

        // Validate method (Sync)
        sb.Append(innerIndent).AppendLine("/// <inheritdoc />");
        sb.Append(innerIndent).Append("public global::eQuantic.Validation.ValidationResult Validate(")
            .Append(model.FullModelTypeName).AppendLine(" instance, global::eQuantic.Validation.ValidationContext? context = null)");
        sb.Append(innerIndent).AppendLine("{");
        sb.Append(innerIndent).AppendLine("    if (instance is null)");
        sb.Append(innerIndent).AppendLine("    {");
        sb.Append(innerIndent).AppendLine("        return global::eQuantic.Validation.ValidationResult.Failure(");
        sb.Append(innerIndent).AppendLine("            new global::eQuantic.Validation.ValidationFailure(string.Empty, global::eQuantic.Validation.ValidationCodes.Required, \"The instance is required.\"));");
        sb.Append(innerIndent).AppendLine("    }");
        sb.AppendLine();
        sb.Append(innerIndent).AppendLine("    var effectiveContext = context ?? global::eQuantic.Validation.ValidationContext.Default;");
        sb.Append(innerIndent).AppendLine("    var failures = new global::System.Collections.Generic.List<global::eQuantic.Validation.ValidationFailure>();");
        sb.AppendLine();

        foreach (var prop in model.Properties)
        {
            GeneratePropertyValidationSync(sb, innerIndent + "    ", prop, model);
        }

        sb.Append(innerIndent).AppendLine("    return failures.Count == 0 ? global::eQuantic.Validation.ValidationResult.Success : new global::eQuantic.Validation.ValidationResult(failures);");
        sb.Append(innerIndent).AppendLine("}");
        sb.AppendLine();

        // ValidateAsync method (Async)
        sb.Append(innerIndent).AppendLine("/// <inheritdoc />");
        sb.Append(innerIndent).Append("public async global::System.Threading.Tasks.Task<global::eQuantic.Validation.ValidationResult> ValidateAsync(")
            .Append(model.FullModelTypeName).AppendLine(" instance, global::eQuantic.Validation.ValidationContext? context = null, global::System.Threading.CancellationToken cancellationToken = default)");
        sb.Append(innerIndent).AppendLine("{");
        sb.Append(innerIndent).AppendLine("    if (instance is null)");
        sb.Append(innerIndent).AppendLine("    {");
        sb.Append(innerIndent).AppendLine("        return global::eQuantic.Validation.ValidationResult.Failure(");
        sb.Append(innerIndent).AppendLine("            new global::eQuantic.Validation.ValidationFailure(string.Empty, global::eQuantic.Validation.ValidationCodes.Required, \"The instance is required.\"));");
        sb.Append(innerIndent).AppendLine("    }");
        sb.AppendLine();
        sb.Append(innerIndent).AppendLine("    cancellationToken.ThrowIfCancellationRequested();");
        sb.Append(innerIndent).AppendLine("    var effectiveContext = context ?? global::eQuantic.Validation.ValidationContext.Default;");
        sb.Append(innerIndent).AppendLine("    var failures = new global::System.Collections.Generic.List<global::eQuantic.Validation.ValidationFailure>();");
        sb.AppendLine();

        foreach (var prop in model.Properties)
        {
            GeneratePropertyValidationAsync(sb, innerIndent + "    ", prop, model);
        }

        sb.Append(innerIndent).AppendLine("    return failures.Count == 0 ? global::eQuantic.Validation.ValidationResult.Success : new global::eQuantic.Validation.ValidationResult(failures);");
        sb.Append(innerIndent).AppendLine("}");
        sb.AppendLine();

        // Non-generic IValidator interface implementation
        sb.Append(innerIndent).AppendLine("global::eQuantic.Validation.ValidationResult global::eQuantic.Validation.IValidator.Validate(object instance, global::eQuantic.Validation.ValidationContext? context) =>");
        sb.Append(innerIndent).Append("    instance is ").Append(model.FullModelTypeName).AppendLine(" typed ? Validate(typed, context) : throw new global::System.ArgumentException($\"Expected an instance of '{typeof(").Append(model.FullModelTypeName).AppendLine(").FullName}'.\", nameof(instance));");
        sb.AppendLine();

        sb.Append(innerIndent).AppendLine("global::System.Threading.Tasks.Task<global::eQuantic.Validation.ValidationResult> global::eQuantic.Validation.IValidator.ValidateAsync(object instance, global::eQuantic.Validation.ValidationContext? context, global::System.Threading.CancellationToken cancellationToken) =>");
        sb.Append(innerIndent).Append("    instance is ").Append(model.FullModelTypeName).AppendLine(" typed ? ValidateAsync(typed, context, cancellationToken) : throw new global::System.ArgumentException($\"Expected an instance of '{typeof(").Append(model.FullModelTypeName).AppendLine(").FullName}'.\", nameof(instance));");
        sb.AppendLine();

        sb.Append(innerIndent).AppendLine("private static global::eQuantic.Validation.ValidationFailure CreateFailure(global::eQuantic.Validation.ValidationContext context, string path, string code, string defaultTemplate, global::eQuantic.Validation.ValidationSeverity severity)");
        sb.Append(innerIndent).AppendLine("{");
        sb.Append(innerIndent).AppendLine("    var message = context.MessageProvider?.Resolve(new global::eQuantic.Validation.ValidationMessageDescriptor(path, path, code, defaultTemplate, new global::System.Collections.Generic.Dictionary<string, object?>())) ?? defaultTemplate;");
        sb.Append(innerIndent).AppendLine("    return new global::eQuantic.Validation.ValidationFailure(path, code, message, severity);");
        sb.Append(innerIndent).AppendLine("}");

        sb.Append(indent).AppendLine("}");

        if (!string.IsNullOrEmpty(model.Namespace))
        {
            sb.AppendLine("}");
        }

        context.AddSource($"{model.ModelName}.GeneratedValidator.g.cs", SourceText.From(sb.ToString(), Encoding.UTF8));
    }

    private static void GeneratePropertyValidationSync(StringBuilder sb, string indent, PropertyValidationDescriptor prop, ModelValidationDescriptor model)
    {
        sb.Append(indent).Append("// ---- Property: ").Append(prop.PropertyName).AppendLine(" ----");
        sb.Append(indent).Append("if (effectiveContext.IncludesPath(\"").Append(prop.PropertyName).AppendLine("\"))");
        sb.Append(indent).AppendLine("{");

        var propIndent = indent + "    ";
        sb.Append(propIndent).Append("var val = instance.").Append(prop.PropertyName).AppendLine(";");

        foreach (var rule in prop.Rules)
        {
            GenerateRuleCheckSync(sb, propIndent, prop, rule, model);
        }

        sb.Append(indent).AppendLine("}");
        sb.AppendLine();
    }

    private static void GeneratePropertyValidationAsync(StringBuilder sb, string indent, PropertyValidationDescriptor prop, ModelValidationDescriptor model)
    {
        sb.Append(indent).Append("// ---- Property: ").Append(prop.PropertyName).AppendLine(" ----");
        sb.Append(indent).Append("if (effectiveContext.IncludesPath(\"").Append(prop.PropertyName).AppendLine("\"))");
        sb.Append(indent).AppendLine("{");

        var propIndent = indent + "    ";
        sb.Append(propIndent).Append("var val = instance.").Append(prop.PropertyName).AppendLine(";");

        foreach (var rule in prop.Rules)
        {
            GenerateRuleCheckAsync(sb, propIndent, prop, rule, model);
        }

        sb.Append(indent).AppendLine("}");
        sb.AppendLine();
    }

    private static void GenerateRuleCheckSync(StringBuilder sb, string indent, PropertyValidationDescriptor prop, RuleDescriptor rule, ModelValidationDescriptor model)
    {
        var scenarioCondition = BuildScenarioCondition(rule.Scenarios);
        if (!string.IsNullOrEmpty(scenarioCondition))
        {
            sb.Append(indent).Append("if (").Append(scenarioCondition).AppendLine(")");
            sb.Append(indent).AppendLine("{");
            indent += "    ";
        }

        var propName = prop.PropertyName;
        var code = EscapeString(rule.Code);
        var msg = EscapeString(rule.Message.Replace("{Property}", propName));
        var sev = rule.Severity switch { 1 => "global::eQuantic.Validation.ValidationSeverity.Warning", 2 => "global::eQuantic.Validation.ValidationSeverity.Info", _ => "global::eQuantic.Validation.ValidationSeverity.Error" };

        switch (rule.Kind)
        {
            case RuleKind.Required:
                if (prop.TypeName.TrimEnd('?') == "string")
                {
                    sb.Append(indent).AppendLine("if (string.IsNullOrWhiteSpace(val))");
                }
                else if (prop.IsNullableOrReference)
                {
                    sb.Append(indent).AppendLine("if (val is null)");
                }
                else
                {
                    sb.Append(indent).AppendLine("if (val == default)");
                }
                sb.Append(indent).Append("    failures.Add(CreateFailure(effectiveContext, \"").Append(propName).Append("\", \"").Append(code).Append("\", \"").Append(msg).Append("\", ").Append(sev).AppendLine("));");
                break;

            case RuleKind.NotEmpty:
                if (prop.TypeName.TrimEnd('?') == "string")
                {
                    sb.Append(indent).AppendLine("if (string.IsNullOrEmpty(val))");
                }
                else if (prop.IsNullableOrReference)
                {
                    sb.Append(indent).AppendLine("if (val is null || (val is global::System.Collections.IEnumerable enumerable && !enumerable.GetEnumerator().MoveNext()))");
                }
                else
                {
                    sb.Append(indent).AppendLine("if (global::System.Collections.Generic.EqualityComparer<").Append(prop.TypeName).AppendLine(">.Default.Equals(val, default!))");
                }
                sb.Append(indent).Append("    failures.Add(CreateFailure(effectiveContext, \"").Append(propName).Append("\", \"").Append(code).Append("\", \"").Append(msg).Append("\", ").Append(sev).AppendLine("));");
                break;

            case RuleKind.NotWhiteSpace:
                sb.Append(indent).AppendLine("if (val is null || string.IsNullOrWhiteSpace(val as string))");
                sb.Append(indent).Append("    failures.Add(CreateFailure(effectiveContext, \"").Append(propName).Append("\", \"").Append(code).Append("\", \"").Append(msg).Append("\", ").Append(sev).AppendLine("));");
                break;

            case RuleKind.Email:
                if (prop.TypeName.TrimEnd('?') == "string")
                {
                    sb.Append(indent).AppendLine("if (!string.IsNullOrWhiteSpace(val) && !global::System.Text.RegularExpressions.Regex.IsMatch(val, @\"^[^\\s@]+@[^\\s@]+\\.[^\\s@]+$\"))");
                }
                else
                {
                    sb.Append(indent).AppendLine("if (val is string emailText && (string.IsNullOrWhiteSpace(emailText) || !global::System.Text.RegularExpressions.Regex.IsMatch(emailText, @\"^[^\\s@]+@[^\\s@]+\\.[^\\s@]+$\")))");
                }
                sb.Append(indent).Append("    failures.Add(CreateFailure(effectiveContext, \"").Append(propName).Append("\", \"").Append(code).Append("\", \"").Append(msg).Append("\", ").Append(sev).AppendLine("));");
                break;

            case RuleKind.MinLength:
                var minLen = rule.Arguments.TryGetValue("MinimumLength", out var ml) ? ml : 0;
                var mlMsg = EscapeString(rule.Message.Replace("{Property}", propName).Replace("{MinimumLength}", minLen?.ToString() ?? "0"));
                if (prop.TypeName.TrimEnd('?') == "string")
                {
                    sb.Append(indent).Append("if (val is not null && val.Length < ").Append(minLen).AppendLine(")");
                }
                else
                {
                    sb.Append(indent).Append("if (val is global::System.Collections.ICollection colMin && colMin.Count < ").Append(minLen).AppendLine(")");
                }
                sb.Append(indent).Append("    failures.Add(CreateFailure(effectiveContext, \"").Append(propName).Append("\", \"").Append(code).Append("\", \"").Append(mlMsg).Append("\", ").Append(sev).AppendLine("));");
                break;

            case RuleKind.MaxLength:
                var maxLen = rule.Arguments.TryGetValue("MaximumLength", out var mxl) ? mxl : 0;
                var mxlMsg = EscapeString(rule.Message.Replace("{Property}", propName).Replace("{MaximumLength}", maxLen?.ToString() ?? "0"));
                if (prop.TypeName.TrimEnd('?') == "string")
                {
                    sb.Append(indent).Append("if (val is not null && val.Length > ").Append(maxLen).AppendLine(")");
                }
                else
                {
                    sb.Append(indent).Append("if (val is global::System.Collections.ICollection colMax && colMax.Count > ").Append(maxLen).AppendLine(")");
                }
                sb.Append(indent).Append("    failures.Add(CreateFailure(effectiveContext, \"").Append(propName).Append("\", \"").Append(code).Append("\", \"").Append(mxlMsg).Append("\", ").Append(sev).AppendLine("));");
                break;

            case RuleKind.Length:
                var lenMin = rule.Arguments.TryGetValue("MinimumLength", out var lmn) ? lmn : 0;
                var lenMax = rule.Arguments.TryGetValue("MaximumLength", out var lmx) ? lmx : 0;
                var lenMsg = EscapeString(rule.Message.Replace("{Property}", propName).Replace("{MinimumLength}", lenMin?.ToString() ?? "0").Replace("{MaximumLength}", lenMax?.ToString() ?? "0"));
                if (prop.TypeName.TrimEnd('?') == "string")
                {
                    sb.Append(indent).Append("if (val is not null && (val.Length < ").Append(lenMin).Append(" || val.Length > ").Append(lenMax).AppendLine("))");
                }
                else
                {
                    sb.Append(indent).Append("if (val is global::System.Collections.ICollection colLen && (colLen.Count < ").Append(lenMin).Append(" || colLen.Count > ").Append(lenMax).AppendLine("))");
                }
                sb.Append(indent).Append("    failures.Add(CreateFailure(effectiveContext, \"").Append(propName).Append("\", \"").Append(code).Append("\", \"").Append(lenMsg).Append("\", ").Append(sev).AppendLine("));");
                break;

            case RuleKind.Range:
                var rMin = rule.Arguments.TryGetValue("Minimum", out var rm) ? rm : 0;
                var rMax = rule.Arguments.TryGetValue("Maximum", out var rmx) ? rmx : 0;
                var rngMsg = EscapeString(rule.Message.Replace("{Property}", propName).Replace("{Minimum}", rMin?.ToString() ?? "0").Replace("{Maximum}", rMax?.ToString() ?? "0"));
                sb.Append(indent).Append("if (val < ").Append(rMin).Append(" || val > ").Append(rMax).AppendLine(")");
                sb.Append(indent).Append("    failures.Add(CreateFailure(effectiveContext, \"").Append(propName).Append("\", \"").Append(code).Append("\", \"").Append(rngMsg).Append("\", ").Append(sev).AppendLine("));");
                break;

            case RuleKind.Pattern:
                var pattern = rule.Arguments.TryGetValue("Pattern", out var pat) ? pat?.ToString() ?? "" : "";
                if (prop.TypeName.TrimEnd('?') == "string")
                {
                    sb.Append(indent).Append("if (val is not null && !global::System.Text.RegularExpressions.Regex.IsMatch(val, @\"").Append(pattern.Replace("\"", "\"\"")).AppendLine("\"))");
                }
                else
                {
                    sb.Append(indent).Append("if (val is string strPat && !global::System.Text.RegularExpressions.Regex.IsMatch(strPat, @\"").Append(pattern.Replace("\"", "\"\"")).AppendLine("\"))");
                }
                sb.Append(indent).Append("    failures.Add(CreateFailure(effectiveContext, \"").Append(propName).Append("\", \"").Append(code).Append("\", \"").Append(msg).Append("\", ").Append(sev).AppendLine("));");
                break;

            case RuleKind.GreaterThan:
                var gtVal = rule.Arguments.TryGetValue("Minimum", out var gtv) ? gtv : 0;
                var gtMsg = EscapeString(rule.Message.Replace("{Property}", propName).Replace("{Minimum}", gtVal?.ToString() ?? "0"));
                sb.Append(indent).Append("if (val <= ").Append(gtVal).AppendLine(")");
                sb.Append(indent).Append("    failures.Add(CreateFailure(effectiveContext, \"").Append(propName).Append("\", \"").Append(code).Append("\", \"").Append(gtMsg).Append("\", ").Append(sev).AppendLine("));");
                break;

            case RuleKind.LessThan:
                var ltVal = rule.Arguments.TryGetValue("Maximum", out var ltv) ? ltv : 0;
                var ltMsg = EscapeString(rule.Message.Replace("{Property}", propName).Replace("{Maximum}", ltVal?.ToString() ?? "0"));
                sb.Append(indent).Append("if (val >= ").Append(ltVal).AppendLine(")");
                sb.Append(indent).Append("    failures.Add(CreateFailure(effectiveContext, \"").Append(propName).Append("\", \"").Append(code).Append("\", \"").Append(ltMsg).Append("\", ").Append(sev).AppendLine("));");
                break;

            case RuleKind.ValidateNested:
                sb.Append(indent).AppendLine("if (val is not null)");
                sb.Append(indent).AppendLine("{");
                sb.Append(indent).Append("    var childValidator = effectiveContext.GetService<global::eQuantic.Validation.IValidator<").Append(prop.TypeName).AppendLine(">>();");
                sb.Append(indent).AppendLine("    if (childValidator is not null)");
                sb.Append(indent).AppendLine("    {");
                sb.Append(indent).Append("        var childResult = childValidator.Validate(val, effectiveContext.CreateChildScope(\"").Append(propName).AppendLine("\"));");
                sb.Append(indent).AppendLine("        foreach (var failure in childResult.Failures)");
                sb.Append(indent).AppendLine("        {");
                sb.Append(indent).Append("            failures.Add(failure.WithPathPrefix(\"").Append(propName).AppendLine("\"));");
                sb.Append(indent).AppendLine("        }");
                sb.Append(indent).AppendLine("    }");
                sb.Append(indent).AppendLine("}");
                break;

            case RuleKind.ValidateEach:
                sb.Append(indent).AppendLine("if (val is global::System.Collections.IEnumerable elements)");
                sb.Append(indent).AppendLine("{");
                sb.Append(indent).AppendLine("    var index = 0;");
                sb.Append(indent).AppendLine("    foreach (var element in elements)");
                sb.Append(indent).AppendLine("    {");
                sb.Append(indent).Append("        var itemPath = $\"").Append(propName).AppendLine("[{index}]\";");
                sb.Append(indent).AppendLine("        if (element is not null && effectiveContext.IncludesPath(itemPath))");
                sb.Append(indent).AppendLine("        {");
                sb.Append(indent).AppendLine("            var itemValidator = effectiveContext.GetService(typeof(global::eQuantic.Validation.IValidator<>).MakeGenericType(element.GetType())) as global::eQuantic.Validation.IValidator;");
                sb.Append(indent).AppendLine("            if (itemValidator is not null)");
                sb.Append(indent).AppendLine("            {");
                sb.Append(indent).AppendLine("                var itemResult = itemValidator.Validate(element, effectiveContext.CreateChildScope(itemPath));");
                sb.Append(indent).AppendLine("                foreach (var failure in itemResult.Failures)");
                sb.Append(indent).AppendLine("                {");
                sb.Append(indent).AppendLine("                    failures.Add(failure.WithPathPrefix(itemPath));");
                sb.Append(indent).AppendLine("                }");
                sb.Append(indent).AppendLine("            }");
                sb.Append(indent).AppendLine("        }");
                sb.Append(indent).AppendLine("        index++;");
                sb.Append(indent).AppendLine("    }");
                sb.Append(indent).AppendLine("}");
                break;

            case RuleKind.CustomRule:
                var method = rule.Arguments.TryGetValue("MethodName", out var mn) ? mn?.ToString() : "";
                var declType = rule.Arguments.TryGetValue("DeclaringType", out var dt) ? dt?.ToString() : null;
                if (!string.IsNullOrEmpty(method))
                {
                    if (string.IsNullOrEmpty(declType))
                    {
                        sb.Append(indent).Append("if (!instance.").Append(method).AppendLine("(val))");
                    }
                    else
                    {
                        sb.Append(indent).Append("if (!").Append(declType).Append(".").Append(method).AppendLine("(instance, val))");
                    }
                    sb.Append(indent).Append("    failures.Add(CreateFailure(effectiveContext, \"").Append(propName).Append("\", \"").Append(code).Append("\", \"").Append(msg).Append("\", ").Append(sev).AppendLine("));");
                }
                break;
        }

        if (!string.IsNullOrEmpty(scenarioCondition))
        {
            sb.Append(indent.Substring(4)).AppendLine("}");
        }
    }

    private static void GenerateRuleCheckAsync(StringBuilder sb, string indent, PropertyValidationDescriptor prop, RuleDescriptor rule, ModelValidationDescriptor model)
    {
        var scenarioCondition = BuildScenarioCondition(rule.Scenarios);
        if (!string.IsNullOrEmpty(scenarioCondition))
        {
            sb.Append(indent).Append("if (").Append(scenarioCondition).AppendLine(")");
            sb.Append(indent).AppendLine("{");
            indent += "    ";
        }

        var propName = prop.PropertyName;
        var code = EscapeString(rule.Code);
        var msg = EscapeString(rule.Message.Replace("{Property}", propName));
        var sev = rule.Severity switch { 1 => "global::eQuantic.Validation.ValidationSeverity.Warning", 2 => "global::eQuantic.Validation.ValidationSeverity.Info", _ => "global::eQuantic.Validation.ValidationSeverity.Error" };

        switch (rule.Kind)
        {
            case RuleKind.ValidateNested:
                sb.Append(indent).AppendLine("if (val is not null)");
                sb.Append(indent).AppendLine("{");
                sb.Append(indent).Append("    var childValidator = effectiveContext.GetService<global::eQuantic.Validation.IValidator<").Append(prop.TypeName).AppendLine(">>();");
                sb.Append(indent).AppendLine("    if (childValidator is not null)");
                sb.Append(indent).AppendLine("    {");
                sb.Append(indent).Append("        var childResult = await childValidator.ValidateAsync(val, effectiveContext.CreateChildScope(\"").Append(propName).AppendLine("\"), cancellationToken).ConfigureAwait(false);");
                sb.Append(indent).AppendLine("        foreach (var failure in childResult.Failures)");
                sb.Append(indent).AppendLine("        {");
                sb.Append(indent).Append("            failures.Add(failure.WithPathPrefix(\"").Append(propName).AppendLine("\"));");
                sb.Append(indent).AppendLine("        }");
                sb.Append(indent).AppendLine("    }");
                sb.Append(indent).AppendLine("}");
                break;

            case RuleKind.ValidateEach:
                sb.Append(indent).AppendLine("if (val is global::System.Collections.IEnumerable elements)");
                sb.Append(indent).AppendLine("{");
                sb.Append(indent).AppendLine("    var index = 0;");
                sb.Append(indent).AppendLine("    foreach (var element in elements)");
                sb.Append(indent).AppendLine("    {");
                sb.Append(indent).AppendLine("        cancellationToken.ThrowIfCancellationRequested();");
                sb.Append(indent).Append("        var itemPath = $\"").Append(propName).AppendLine("[{index}]\";");
                sb.Append(indent).AppendLine("        if (element is not null && effectiveContext.IncludesPath(itemPath))");
                sb.Append(indent).AppendLine("        {");
                sb.Append(indent).AppendLine("            var itemValidator = effectiveContext.GetService(typeof(global::eQuantic.Validation.IValidator<>).MakeGenericType(element.GetType())) as global::eQuantic.Validation.IValidator;");
                sb.Append(indent).AppendLine("            if (itemValidator is not null)");
                sb.Append(indent).AppendLine("            {");
                sb.Append(indent).AppendLine("                var itemResult = await itemValidator.ValidateAsync(element, effectiveContext.CreateChildScope(itemPath), cancellationToken).ConfigureAwait(false);");
                sb.Append(indent).AppendLine("                foreach (var failure in itemResult.Failures)");
                sb.Append(indent).AppendLine("                {");
                sb.Append(indent).AppendLine("                    failures.Add(failure.WithPathPrefix(itemPath));");
                sb.Append(indent).AppendLine("                }");
                sb.Append(indent).AppendLine("            }");
                sb.Append(indent).AppendLine("        }");
                sb.Append(indent).AppendLine("        index++;");
                sb.Append(indent).AppendLine("    }");
                sb.Append(indent).AppendLine("}");
                break;

            default:
                // Sync rule fallback in async method
                GenerateRuleCheckSync(sb, indent, prop, rule, model);
                break;
        }

        if (!string.IsNullOrEmpty(scenarioCondition))
        {
            sb.Append(indent.Substring(4)).AppendLine("}");
        }
    }

    private static string BuildScenarioCondition(string[]? scenarios)
    {
        if (scenarios is null || scenarios.Length == 0)
        {
            return string.Empty;
        }

        var sb = new StringBuilder();
        for (var i = 0; i < scenarios.Length; i++)
        {
            if (i > 0)
            {
                sb.Append(" || ");
            }
            sb.Append("effectiveContext.Scenarios.Contains(\"").Append(EscapeString(scenarios[i])).Append("\")");
        }
        return sb.ToString();
    }

    private static string EscapeString(string value) =>
        value.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\r", "").Replace("\n", "");

    private static void GenerateRegistrations(
        SourceProductionContext context,
        Compilation compilation,
        ImmutableArray<ValidatorDescriptor> manualValidators,
        ImmutableArray<ModelValidationDescriptor> generatedModels)
    {
        var allDescriptors = new List<ValidatorDescriptor>();

        // Add manual validators
        allDescriptors.AddRange(manualValidators);

        // Add generated validators
        foreach (var model in generatedModels)
        {
            allDescriptors.Add(new ValidatorDescriptor(model.FullGeneratedValidatorTypeName, model.FullModelTypeName));
        }

        // Add referenced validators
        allDescriptors.AddRange(DiscoverReferencedValidators(compilation));

        var distinctValidators = allDescriptors
            .Distinct(ValidatorDescriptorComparer.Instance)
            .OrderBy(static descriptor => descriptor.ValidatorType, StringComparer.Ordinal)
            .ToArray();

        if (distinctValidators.Length == 0)
        {
            return;
        }

        if (compilation.GetTypeByMetadataName(AspNetCoreExtensionsType) is null ||
            compilation.GetTypeByMetadataName(ServiceCollectionType) is null)
        {
            context.ReportDiagnostic(Diagnostic.Create(MissingAspNetCoreIntegration, Location.None));
            return;
        }

        context.AddSource(
            "Validation.GeneratedRegistrations.g.cs",
            SourceText.From(CreateRegistrationSource(distinctValidators), Encoding.UTF8));
    }

    private static string CreateRegistrationSource(IReadOnlyList<ValidatorDescriptor> validators)
    {
        var source = new StringBuilder();
        source.AppendLine("// <auto-generated />");
        source.AppendLine("#nullable enable");
        source.AppendLine();
        source.AppendLine("namespace eQuantic.Validation.Generated;");
        source.AppendLine();
        source.AppendLine("/// <summary>Registers validators discovered or generated at compile time in this application and its references.</summary>");
        source.AppendLine("public static class ValidationGeneratedExtensions");
        source.AppendLine("{");
        source.AppendLine("    /// <summary>Registers generated validators as scoped services.</summary>");
        source.AppendLine("    public static global::Microsoft.Extensions.DependencyInjection.IServiceCollection AddGeneratedValidation(");
        source.AppendLine("        this global::Microsoft.Extensions.DependencyInjection.IServiceCollection services)");
        source.AppendLine("    {");
        source.AppendLine("        if (services is null) throw new global::System.ArgumentNullException(nameof(services));");
        source.AppendLine("        global::eQuantic.Validation.AspNetCore.ServiceCollectionExtensions.AddValidation(services);");

        foreach (var validator in validators)
        {
            source.Append("        global::eQuantic.Validation.AspNetCore.ServiceCollectionExtensions.AddValidator<")
                .Append(validator.ModelType)
                .Append(", ")
                .Append(validator.ValidatorType)
                .AppendLine(">(services);");
        }

        source.AppendLine("        return services;");
        source.AppendLine("    }");
        source.AppendLine("}");

        return source.ToString();
    }

    private static bool IsAccessible(INamedTypeSymbol type, bool requirePublic)
    {
        for (INamedTypeSymbol? current = type; current is not null; current = current.ContainingType)
        {
            if ((requirePublic && current.DeclaredAccessibility != Accessibility.Public) ||
                (!requirePublic && current.DeclaredAccessibility is not (Accessibility.Public or Accessibility.Internal)))
            {
                return false;
            }
        }

        return true;
    }

    private static IEnumerable<ValidatorDescriptor> DiscoverReferencedValidators(Compilation compilation)
    {
        foreach (var reference in compilation.References)
        {
            if (compilation.GetAssemblyOrModuleSymbol(reference) is not IAssemblySymbol assembly)
            {
                continue;
            }

            foreach (var type in GetTypes(assembly.GlobalNamespace))
            {
                var descriptor = CreateManualValidatorDescriptor(type, requirePublic: true);
                if (descriptor is not null)
                {
                    yield return descriptor;
                }
            }
        }
    }

    private static IEnumerable<INamedTypeSymbol> GetTypes(INamespaceSymbol @namespace)
    {
        foreach (var type in @namespace.GetTypeMembers())
        {
            yield return type;
            foreach (var nestedType in GetNestedTypes(type))
            {
                yield return nestedType;
            }
        }

        foreach (var childNamespace in @namespace.GetNamespaceMembers())
        {
            foreach (var type in GetTypes(childNamespace))
            {
                yield return type;
            }
        }
    }

    private static IEnumerable<INamedTypeSymbol> GetNestedTypes(INamedTypeSymbol type)
    {
        foreach (var nestedType in type.GetTypeMembers())
        {
            yield return nestedType;
            foreach (var descendant in GetNestedTypes(nestedType))
            {
                yield return descendant;
            }
        }
    }

    private static readonly DiagnosticDescriptor MissingAspNetCoreIntegration = new(
        id: "VALGEN001",
        title: "ASP.NET Core integration is required for generated registrations",
        messageFormat: "Reference eQuantic.Validation.AspNetCore to use generated validator registrations",
        category: "Validation",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "The generated method calls validation registration extensions.");

    private enum RuleKind
    {
        Required,
        NotEmpty,
        NotWhiteSpace,
        Email,
        MinLength,
        MaxLength,
        Length,
        Range,
        Pattern,
        GreaterThan,
        LessThan,
        ValidateNested,
        ValidateEach,
        CustomRule
    }

    private sealed class ModelValidationDescriptor
    {
        public ModelValidationDescriptor(
            string modelName,
            string @namespace,
            string fullModelTypeName,
            string generatedValidatorName,
            string fullGeneratedValidatorTypeName,
            IReadOnlyList<PropertyValidationDescriptor> properties)
        {
            ModelName = modelName;
            Namespace = @namespace;
            FullModelTypeName = fullModelTypeName;
            GeneratedValidatorName = generatedValidatorName;
            FullGeneratedValidatorTypeName = fullGeneratedValidatorTypeName;
            Properties = properties;
        }

        public string ModelName { get; }
        public string Namespace { get; }
        public string FullModelTypeName { get; }
        public string GeneratedValidatorName { get; }
        public string FullGeneratedValidatorTypeName { get; }
        public IReadOnlyList<PropertyValidationDescriptor> Properties { get; }
    }

    private sealed class PropertyValidationDescriptor
    {
        public PropertyValidationDescriptor(
            string propertyName,
            string typeName,
            bool isNullableOrReference,
            IReadOnlyList<RuleDescriptor> rules)
        {
            PropertyName = propertyName;
            TypeName = typeName;
            IsNullableOrReference = isNullableOrReference;
            Rules = rules;
        }

        public string PropertyName { get; }
        public string TypeName { get; }
        public bool IsNullableOrReference { get; }
        public IReadOnlyList<RuleDescriptor> Rules { get; }
    }

    private sealed class RuleDescriptor
    {
        public RuleDescriptor(
            RuleKind kind,
            string code,
            string message,
            int severity,
            string[]? scenarios,
            IImmutableDictionary<string, object?> arguments)
        {
            Kind = kind;
            Code = code;
            Message = message;
            Severity = severity;
            Scenarios = scenarios;
            Arguments = arguments;
        }

        public RuleKind Kind { get; }
        public string Code { get; }
        public string Message { get; }
        public int Severity { get; }
        public string[]? Scenarios { get; }
        public IImmutableDictionary<string, object?> Arguments { get; }
    }

    private sealed class ValidatorDescriptor
    {
        public ValidatorDescriptor(string validatorType, string modelType)
        {
            ValidatorType = validatorType;
            ModelType = modelType;
        }

        public string ValidatorType { get; }
        public string ModelType { get; }
    }

    private sealed class ValidatorDescriptorComparer : IEqualityComparer<ValidatorDescriptor>
    {
        public static ValidatorDescriptorComparer Instance { get; } = new();

        public bool Equals(ValidatorDescriptor? left, ValidatorDescriptor? right)
        {
            return ReferenceEquals(left, right) ||
                   left is not null &&
                   right is not null &&
                   string.Equals(left.ValidatorType, right.ValidatorType, StringComparison.Ordinal);
        }

        public int GetHashCode(ValidatorDescriptor descriptor) =>
            StringComparer.Ordinal.GetHashCode(descriptor.ValidatorType);
    }
}
