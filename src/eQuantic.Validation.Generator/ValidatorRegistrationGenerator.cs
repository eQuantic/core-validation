using System.Globalization;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace eQuantic.Validation.Generator;

/// <summary>
/// Incremental generator that produces reflection-free validators for types annotated with
/// <c>[GenerateValidator]</c> and explicit DI registrations for every validator declared in the
/// compiling assembly. Referenced assemblies are intentionally not scanned: registration is
/// opt-in per assembly, keeping the pipeline incremental and free of surprise registrations.
/// </summary>
[Generator(LanguageNames.CSharp)]
public sealed class ValidatorRegistrationGenerator : IIncrementalGenerator
{
    private const string ValidatorNamespace = "eQuantic.Validation";
    private const string ValidatorInterfaceName = "IValidator`1";
    private const string GenerateValidatorAttributeName = "eQuantic.Validation.Attributes.GenerateValidatorAttribute";
    private const string CoreExtensionsType = "eQuantic.Validation.ServiceCollectionExtensions";
    private const string ServiceCollectionType = "Microsoft.Extensions.DependencyInjection.IServiceCollection";

    /// <summary>Fully qualified display including nullable reference annotations, so generated
    /// accessor delegates match the nullability of the source expressions (no CS86xx warnings).</summary>
    private static readonly SymbolDisplayFormat FullyQualifiedNullableFormat =
        SymbolDisplayFormat.FullyQualifiedFormat.AddMiscellaneousOptions(
            SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier);

    /// <inheritdoc />
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var models = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                GenerateValidatorAttributeName,
                static (node, _) => node is TypeDeclarationSyntax,
                static (syntaxContext, cancellationToken) => CreateModelDescriptor(syntaxContext, cancellationToken))
            .Where(static descriptor => descriptor is not null)
            .Select(static (descriptor, _) => descriptor!);

        var manualValidators = context.SyntaxProvider
            .CreateSyntaxProvider(
                static (node, _) => node is ClassDeclarationSyntax { BaseList: not null },
                static (syntaxContext, cancellationToken) => CreateManualValidatorDescriptor(syntaxContext, cancellationToken))
            .Where(static descriptor => descriptor is not null)
            .Select(static (descriptor, _) => descriptor!);

        // Cheap capability flags projected from the compilation; the booleans have value
        // equality, so downstream registration output stays cached until they actually change.
        var capabilities = context.CompilationProvider.Select(static (compilation, _) =>
            new RegistrationCapabilities(
                compilation.GetTypeByMetadataName(CoreExtensionsType) is not null,
                compilation.GetTypeByMetadataName(ServiceCollectionType) is not null,
                compilation.GetTypeByMetadataName("eQuantic.Validation.ValidatorAccessors") is not null,
                compilation.GetTypeByMetadataName("System.Runtime.CompilerServices.ModuleInitializerAttribute") is not null));

        // Property accessors used by fluent RuleFor/RuleForEach calls, pre-compiled at module
        // load so the fluent path skips Expression.Compile (per-instantiation on JIT, interpreted
        // under Native AOT).
        var accessorRegistrations = context.SyntaxProvider
            .CreateSyntaxProvider(
                static (node, _) => node is ClassDeclarationSyntax { BaseList: not null },
                static (syntaxContext, cancellationToken) => CreateAccessorDescriptor(syntaxContext, cancellationToken))
            .Where(static descriptor => descriptor is not null)
            .Select(static (descriptor, _) => descriptor!);

        context.RegisterSourceOutput(models, static (productionContext, model) =>
            ValidatorEmitter.EmitModelValidator(productionContext, model));

        var registrationInputs = manualValidators.Collect().Combine(models.Collect()).Combine(capabilities);
        context.RegisterSourceOutput(registrationInputs, static (productionContext, source) =>
        {
            var ((manuals, generatedModels), registrationCapabilities) = source;
            ValidatorEmitter.EmitRegistrations(productionContext, manuals, generatedModels, registrationCapabilities);
        });

        var accessorInputs = accessorRegistrations.Collect().Combine(capabilities);
        context.RegisterSourceOutput(accessorInputs, static (productionContext, source) =>
        {
            var (descriptors, registrationCapabilities) = source;
            ValidatorEmitter.EmitAccessorRegistrations(productionContext, descriptors, registrationCapabilities);
        });
    }

    private static AccessorRegistrationDescriptor? CreateAccessorDescriptor(
        GeneratorSyntaxContext context,
        CancellationToken cancellationToken)
    {
        if (context.SemanticModel.GetDeclaredSymbol(context.Node, cancellationToken) is not INamedTypeSymbol validatorType ||
            validatorType.IsGenericType)
        {
            return null;
        }

        // Only fluent validators (subclasses of Validator<TModel>) declare RuleFor accessors.
        INamedTypeSymbol? modelType = null;
        for (var current = validatorType.BaseType; current is not null; current = current.BaseType)
        {
            if (current.OriginalDefinition.MetadataName == "Validator`1" &&
                current.OriginalDefinition.ContainingNamespace.ToDisplayString() == ValidatorNamespace)
            {
                modelType = current.TypeArguments[0] as INamedTypeSymbol;
                break;
            }
        }

        if (modelType is null || modelType.TypeKind == TypeKind.Error || !IsExternallyReachable(modelType))
        {
            return null;
        }

        var modelTypeName = modelType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        var entries = new List<AccessorEntry>();
        var seenPaths = new HashSet<string>(StringComparer.Ordinal);

        foreach (var invocation in context.Node.DescendantNodes().OfType<InvocationExpressionSyntax>())
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Only the validator's own RuleFor/RuleForEach calls (implicit or explicit this)
            // target TModel; receiver calls such as `child.RuleFor(...)` inside ChildRules
            // lambdas target other models and must not be registered against TModel.
            var methodName = invocation.Expression switch
            {
                IdentifierNameSyntax identifier => identifier.Identifier.ValueText,
                MemberAccessExpressionSyntax { Expression: ThisExpressionSyntax } member =>
                    member.Name.Identifier.ValueText,
                _ => null,
            };

            if (methodName is not ("RuleFor" or "RuleForEach") ||
                invocation.ArgumentList.Arguments.Count != 1)
            {
                continue;
            }

            var lambdaBody = invocation.ArgumentList.Arguments[0].Expression switch
            {
                SimpleLambdaExpressionSyntax simple => simple.ExpressionBody,
                ParenthesizedLambdaExpressionSyntax parenthesized => parenthesized.ExpressionBody,
                _ => null,
            };

            if (lambdaBody is null || !TryGetMemberSegments(lambdaBody, out var segments))
            {
                continue;
            }

            var path = string.Join(".", segments);
            if (path.Length == 0 || !seenPaths.Add(methodName + ":" + path))
            {
                continue;
            }

            if (context.SemanticModel.GetSymbolInfo(Unwrap(lambdaBody), cancellationToken).Symbol
                    is not IPropertySymbol property ||
                !IsExternallyReachable(property.Type))
            {
                continue;
            }

            string valueTypeName;
            if (methodName == "RuleForEach")
            {
                if (context.SemanticModel.GetSymbolInfo(invocation, cancellationToken).Symbol
                        is not IMethodSymbol { TypeArguments.Length: 1 } method ||
                    !IsExternallyReachable(method.TypeArguments[0]))
                {
                    continue;
                }

                valueTypeName = "global::System.Collections.Generic.IEnumerable<" +
                    method.TypeArguments[0].ToDisplayString(FullyQualifiedNullableFormat) + ">?";
            }
            else
            {
                valueTypeName = property.Type.ToDisplayString(FullyQualifiedNullableFormat);
            }

            // Intermediate segments carry '!' so the generated accessor never warns on nullable chains.
            var body = "static x => x." + string.Join("!.", segments);
            entries.Add(new AccessorEntry(path, valueTypeName, body));
        }

        return entries.Count == 0
            ? null
            : new AccessorRegistrationDescriptor(modelTypeName, new EquatableArray<AccessorEntry>(entries.ToArray()));
    }

    private static bool TryGetMemberSegments(ExpressionSyntax body, out List<string> segments)
    {
        segments = new List<string>();
        var current = Unwrap(body);

        while (current is MemberAccessExpressionSyntax memberAccess)
        {
            segments.Insert(0, memberAccess.Name.Identifier.ValueText);
            current = Unwrap(memberAccess.Expression);
        }

        return segments.Count > 0 && current is IdentifierNameSyntax;
    }

    private static ExpressionSyntax Unwrap(ExpressionSyntax expression)
    {
        while (expression is PostfixUnaryExpressionSyntax postfix &&
               postfix.IsKind(SyntaxKind.SuppressNullableWarningExpression))
        {
            expression = postfix.Operand;
        }

        return expression;
    }

    /// <summary>Accessor registrations live in a generated top-level class, so every type they
    /// mention must be reachable from outside its declaring type (no private nested types).</summary>
    private static bool IsExternallyReachable(ITypeSymbol type)
    {
        for (ITypeSymbol? current = type; current is not null; current = current.ContainingType)
        {
            if (current.DeclaredAccessibility is not (Accessibility.Public or Accessibility.Internal or Accessibility.NotApplicable))
            {
                return false;
            }
        }

        return true;
    }

    private static ValidatorDescriptor? CreateManualValidatorDescriptor(
        GeneratorSyntaxContext context,
        CancellationToken cancellationToken)
    {
        if (context.SemanticModel.GetDeclaredSymbol(context.Node, cancellationToken) is not INamedTypeSymbol validatorType)
        {
            return null;
        }

        if (validatorType.IsAbstract || validatorType.IsGenericType || !IsAccessible(validatorType))
        {
            return null;
        }

        var validatorInterface = validatorType.AllInterfaces.FirstOrDefault(static type =>
            type.OriginalDefinition.MetadataName == ValidatorInterfaceName &&
            type.OriginalDefinition.ContainingNamespace.ToDisplayString() == ValidatorNamespace);

        if (validatorInterface is null ||
            validatorInterface.TypeArguments[0] is not INamedTypeSymbol modelType ||
            modelType.TypeKind == TypeKind.Error ||
            !IsAccessible(modelType))
        {
            return null;
        }

        return new ValidatorDescriptor(
            validatorType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
            modelType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat));
    }

    private static ModelDescriptor? CreateModelDescriptor(
        GeneratorAttributeSyntaxContext context,
        CancellationToken cancellationToken)
    {
        if (context.TargetSymbol is not INamedTypeSymbol typeSymbol)
        {
            return null;
        }

        var diagnostics = new List<DiagnosticInfo>();

        if (typeSymbol.IsGenericType)
        {
            diagnostics.Add(DiagnosticInfo.Create(
                ValidatorEmitter.UnsupportedTargetId,
                $"[GenerateValidator] does not support generic type '{typeSymbol.Name}'; declare a validator manually instead.",
                typeSymbol.Locations.FirstOrDefault()));
            return WithOnlyDiagnostics(typeSymbol, diagnostics);
        }

        var properties = new List<PropertyDescriptor>();
        foreach (var member in typeSymbol.GetMembers())
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (member is not IPropertySymbol property ||
                property.IsStatic ||
                property.DeclaredAccessibility != Accessibility.Public ||
                property.GetMethod is null)
            {
                continue;
            }

            var typeName = property.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            var descriptor = new PropertyShape(
                property.Name,
                typeName,
                typeName.TrimEnd('?'),
                property.Type.IsReferenceType || property.Type.NullableAnnotation == NullableAnnotation.Annotated,
                property.Type.SpecialType == SpecialType.System_String,
                GetEnumerableElementType(property.Type));

            var rules = ExtractPropertyRules(property, typeSymbol, descriptor, diagnostics);
            if (rules.Count > 0)
            {
                properties.Add(new PropertyDescriptor(
                    descriptor.Name,
                    descriptor.TypeName,
                    descriptor.BareTypeName,
                    descriptor.IsNullableOrReference,
                    descriptor.IsString,
                    new EquatableArray<RuleDescriptor>(rules.ToArray())));
            }
        }

        var namespaceName = typeSymbol.ContainingNamespace.IsGlobalNamespace
            ? string.Empty
            : typeSymbol.ContainingNamespace.ToDisplayString();

        var generatedValidatorName = typeSymbol.Name + "GeneratedValidator";
        var fullGeneratedValidatorName = string.IsNullOrEmpty(namespaceName)
            ? "global::" + generatedValidatorName
            : "global::" + namespaceName + "." + generatedValidatorName;

        var fullModelTypeName = typeSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

        return new ModelDescriptor(
            typeSymbol.Name,
            namespaceName,
            fullModelTypeName,
            typeSymbol.IsReferenceType,
            generatedValidatorName,
            fullGeneratedValidatorName,
            CreateHintName(fullModelTypeName),
            new EquatableArray<PropertyDescriptor>(properties.ToArray()),
            new EquatableArray<DiagnosticInfo>(diagnostics.ToArray()));
    }

    private static ModelDescriptor WithOnlyDiagnostics(INamedTypeSymbol typeSymbol, List<DiagnosticInfo> diagnostics)
    {
        return new ModelDescriptor(
            typeSymbol.Name,
            string.Empty,
            string.Empty,
            IsReferenceType: true,
            string.Empty,
            string.Empty,
            string.Empty,
            EquatableArray<PropertyDescriptor>.Empty,
            new EquatableArray<DiagnosticInfo>(diagnostics.ToArray()));
    }

    private static string CreateHintName(string fullModelTypeName)
    {
        var sanitized = fullModelTypeName
            .Replace("global::", string.Empty)
            .Replace('.', '_');
        return sanitized + ".GeneratedValidator.g.cs";
    }

    private static string? GetEnumerableElementType(ITypeSymbol type)
    {
        if (type.SpecialType == SpecialType.System_String)
        {
            return null;
        }

        if (type is IArrayTypeSymbol arrayType)
        {
            return arrayType.ElementType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat).TrimEnd('?');
        }

        if (type is INamedTypeSymbol named &&
            named.OriginalDefinition.SpecialType == SpecialType.System_Collections_Generic_IEnumerable_T)
        {
            return named.TypeArguments[0].ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat).TrimEnd('?');
        }

        var enumerableInterface = type.AllInterfaces.FirstOrDefault(static candidate =>
            candidate.OriginalDefinition.SpecialType == SpecialType.System_Collections_Generic_IEnumerable_T);
        return enumerableInterface?.TypeArguments[0]
            .ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)
            .TrimEnd('?');
    }

    private sealed record PropertyShape(
        string Name,
        string TypeName,
        string BareTypeName,
        bool IsNullableOrReference,
        bool IsString,
        string? ElementTypeName);

    private static List<RuleDescriptor> ExtractPropertyRules(
        IPropertySymbol property,
        INamedTypeSymbol containingType,
        PropertyShape shape,
        List<DiagnosticInfo> diagnostics)
    {
        var allAttributes = new List<AttributeData>(property.GetAttributes());

        // Records surface primary-constructor attributes on the parameter, not the property.
        foreach (var constructor in containingType.Constructors)
        {
            var parameter = constructor.Parameters.FirstOrDefault(candidate =>
                string.Equals(candidate.Name, property.Name, StringComparison.OrdinalIgnoreCase));

            if (parameter is not null)
            {
                foreach (var attribute in parameter.GetAttributes())
                {
                    if (!allAttributes.Any(existing =>
                            existing.AttributeClass?.ToDisplayString() == attribute.AttributeClass?.ToDisplayString()))
                    {
                        allAttributes.Add(attribute);
                    }
                }
            }
        }

        var rules = new List<RuleDescriptor>();
        foreach (var attribute in allAttributes)
        {
            var attributeClass = attribute.AttributeClass;
            if (attributeClass is null)
            {
                continue;
            }

            var attributeFullName = attributeClass.ToDisplayString();

            string? code = null;
            string? message = null;
            var severity = 0;
            var scenarios = EquatableArray<string>.Empty;

            foreach (var namedArgument in attribute.NamedArguments)
            {
                switch (namedArgument.Key)
                {
                    case "Code":
                        code = namedArgument.Value.Value?.ToString();
                        break;
                    case "Message":
                        message = namedArgument.Value.Value?.ToString();
                        break;
                    case "Severity":
                        if (namedArgument.Value.Value is int severityValue)
                        {
                            severity = severityValue;
                        }

                        break;
                    case "Scenarios":
                        if (!namedArgument.Value.IsNull && namedArgument.Value.Values.Length > 0)
                        {
                            var values = namedArgument.Value.Values
                                .Select(static value => value.Value?.ToString())
                                .Where(static value => !string.IsNullOrEmpty(value))
                                .Select(static value => value!)
                                .ToArray();
                            scenarios = new EquatableArray<string>(values);
                        }

                        break;
                }
            }

            var rule = CreateRule(attribute, attributeFullName, code, message, severity, scenarios, shape, containingType, diagnostics);
            if (rule is not null)
            {
                rules.Add(rule);
            }
        }

        return rules;
    }

    private static RuleDescriptor? CreateRule(
        AttributeData attribute,
        string attributeFullName,
        string? code,
        string? message,
        int severity,
        EquatableArray<string> scenarios,
        PropertyShape shape,
        INamedTypeSymbol containingType,
        List<DiagnosticInfo> diagnostics)
    {
        switch (attributeFullName)
        {
            case "eQuantic.Validation.Attributes.RequiredAttribute":
            case "System.ComponentModel.DataAnnotations.RequiredAttribute":
                return new RuleDescriptor(
                    RuleKind.Required, code ?? "required", message ?? "{Property} is required.",
                    severity, scenarios, EquatableArray<RuleArgument>.Empty);

            case "eQuantic.Validation.Attributes.NotEmptyAttribute":
                return new RuleDescriptor(
                    RuleKind.NotEmpty, code ?? "not_empty", message ?? "{Property} must not be empty.",
                    severity, scenarios, EquatableArray<RuleArgument>.Empty);

            case "eQuantic.Validation.Attributes.NotWhiteSpaceAttribute":
                if (!RequireString(attribute, shape, "NotWhiteSpace", diagnostics))
                {
                    return null;
                }

                return new RuleDescriptor(
                    RuleKind.NotWhiteSpace, code ?? "not_whitespace", message ?? "{Property} must not be blank.",
                    severity, scenarios, EquatableArray<RuleArgument>.Empty);

            case "eQuantic.Validation.Attributes.EmailAttribute":
            case "System.ComponentModel.DataAnnotations.EmailAddressAttribute":
                if (!RequireString(attribute, shape, "Email", diagnostics))
                {
                    return null;
                }

                return new RuleDescriptor(
                    RuleKind.Email, code ?? "email", message ?? "{Property} must be a valid email address.",
                    severity, scenarios, EquatableArray<RuleArgument>.Empty);

            case "eQuantic.Validation.Attributes.MinLengthAttribute":
            case "System.ComponentModel.DataAnnotations.MinLengthAttribute":
            {
                var length = GetConstructorInt(attribute, 0);
                return new RuleDescriptor(
                    RuleKind.MinLength, code ?? "minimum_length",
                    message ?? "{Property} must contain at least {MinimumLength} characters.",
                    severity, scenarios,
                    Arguments(new RuleArgument("MinimumLength", length.ToString(CultureInfo.InvariantCulture))));
            }

            case "eQuantic.Validation.Attributes.MaxLengthAttribute":
            case "System.ComponentModel.DataAnnotations.MaxLengthAttribute":
            {
                var length = GetConstructorInt(attribute, 0);
                return new RuleDescriptor(
                    RuleKind.MaxLength, code ?? "maximum_length",
                    message ?? "{Property} must contain no more than {MaximumLength} characters.",
                    severity, scenarios,
                    Arguments(new RuleArgument("MaximumLength", length.ToString(CultureInfo.InvariantCulture))));
            }

            case "eQuantic.Validation.Attributes.LengthAttribute":
            case "System.ComponentModel.DataAnnotations.StringLengthAttribute":
            {
                var minimum = 0;
                var maximum = 0;
                if (attribute.ConstructorArguments.Length >= 2)
                {
                    minimum = GetConstructorInt(attribute, 0);
                    maximum = GetConstructorInt(attribute, 1);
                }
                else if (attribute.ConstructorArguments.Length == 1)
                {
                    maximum = GetConstructorInt(attribute, 0);
                    foreach (var namedArgument in attribute.NamedArguments)
                    {
                        if (namedArgument.Key == "MinimumLength" && namedArgument.Value.Value is int minimumValue)
                        {
                            minimum = minimumValue;
                        }
                    }
                }

                return new RuleDescriptor(
                    RuleKind.Length, code ?? "range",
                    message ?? "{Property} must contain between {MinimumLength} and {MaximumLength} characters.",
                    severity, scenarios,
                    Arguments(
                        new RuleArgument("MinimumLength", minimum.ToString(CultureInfo.InvariantCulture)),
                        new RuleArgument("MaximumLength", maximum.ToString(CultureInfo.InvariantCulture))));
            }

            case "eQuantic.Validation.Attributes.RangeAttribute":
            case "System.ComponentModel.DataAnnotations.RangeAttribute":
            {
                if (!RequireNonString(attribute, shape, "Range", diagnostics))
                {
                    return null;
                }

                var minimum = RenderNumericLiteral(GetConstructorValue(attribute, 0), shape.BareTypeName);
                var maximum = RenderNumericLiteral(GetConstructorValue(attribute, 1), shape.BareTypeName);
                return new RuleDescriptor(
                    RuleKind.Range, code ?? "range",
                    message ?? "{Property} must be between {Minimum} and {Maximum}.",
                    severity, scenarios,
                    Arguments(new RuleArgument("Minimum", minimum), new RuleArgument("Maximum", maximum)));
            }

            case "eQuantic.Validation.Attributes.GreaterThanAttribute":
            {
                if (!RequireNonString(attribute, shape, "GreaterThan", diagnostics))
                {
                    return null;
                }

                var minimum = RenderNumericLiteral(GetConstructorValue(attribute, 0), shape.BareTypeName);
                return new RuleDescriptor(
                    RuleKind.GreaterThan, code ?? "range",
                    message ?? "{Property} must be greater than {Minimum}.",
                    severity, scenarios,
                    Arguments(new RuleArgument("Minimum", minimum)));
            }

            case "eQuantic.Validation.Attributes.LessThanAttribute":
            {
                if (!RequireNonString(attribute, shape, "LessThan", diagnostics))
                {
                    return null;
                }

                var maximum = RenderNumericLiteral(GetConstructorValue(attribute, 0), shape.BareTypeName);
                return new RuleDescriptor(
                    RuleKind.LessThan, code ?? "range",
                    message ?? "{Property} must be less than {Maximum}.",
                    severity, scenarios,
                    Arguments(new RuleArgument("Maximum", maximum)));
            }

            case "eQuantic.Validation.Attributes.PatternAttribute":
            case "System.ComponentModel.DataAnnotations.RegularExpressionAttribute":
            {
                if (!RequireString(attribute, shape, "Pattern", diagnostics))
                {
                    return null;
                }

                var pattern = GetConstructorValue(attribute, 0)?.ToString() ?? string.Empty;
                return new RuleDescriptor(
                    RuleKind.Pattern, code ?? "pattern", message ?? "{Property} has an invalid format.",
                    severity, scenarios, EquatableArray<RuleArgument>.Empty,
                    Pattern: pattern);
            }

            case "eQuantic.Validation.Attributes.ValidateNestedAttribute":
                return new RuleDescriptor(
                    RuleKind.ValidateNested, code ?? "nested", message ?? "{Property} is invalid.",
                    severity, scenarios, EquatableArray<RuleArgument>.Empty);

            case "eQuantic.Validation.Attributes.ValidateEachAttribute":
            {
                if (shape.ElementTypeName is null)
                {
                    diagnostics.Add(DiagnosticInfo.Create(
                        ValidatorEmitter.ElementTypeUnresolvedId,
                        $"[ValidateEach] on '{shape.Name}' requires a property implementing IEnumerable<T>; the element type could not be determined.",
                        GetAttributeLocation(attribute)));
                    return null;
                }

                return new RuleDescriptor(
                    RuleKind.ValidateEach, code ?? "collection", message ?? "{Property} is invalid.",
                    severity, scenarios, EquatableArray<RuleArgument>.Empty,
                    ElementTypeName: shape.ElementTypeName);
            }

            case "eQuantic.Validation.Attributes.CustomRuleAttribute":
            {
                string methodName;
                string? declaringType = null;
                ITypeSymbol? declaringTypeSymbol = null;

                if (attribute.ConstructorArguments.Length == 2)
                {
                    declaringTypeSymbol = attribute.ConstructorArguments[0].Value as ITypeSymbol;
                    declaringType = declaringTypeSymbol?.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                    methodName = attribute.ConstructorArguments[1].Value?.ToString() ?? string.Empty;
                }
                else
                {
                    methodName = GetConstructorValue(attribute, 0)?.ToString() ?? string.Empty;
                }

                if (string.IsNullOrEmpty(methodName))
                {
                    return null;
                }

                var methodHost = declaringTypeSymbol ?? containingType;
                var expectStatic = declaringTypeSymbol is not null;
                var found = methodHost.GetMembers(methodName)
                    .OfType<IMethodSymbol>()
                    .Any(candidate =>
                        candidate.IsStatic == expectStatic &&
                        candidate.ReturnType.SpecialType == SpecialType.System_Boolean);

                if (!found)
                {
                    diagnostics.Add(DiagnosticInfo.Create(
                        ValidatorEmitter.CustomRuleMethodMissingId,
                        $"[CustomRule] method '{methodName}' returning bool was not found on '{methodHost.Name}' (expected {(expectStatic ? "a static" : "an instance")} method).",
                        GetAttributeLocation(attribute)));
                    return null;
                }

                return new RuleDescriptor(
                    RuleKind.CustomRule, code ?? "predicate", message ?? "{Property} is invalid.",
                    severity, scenarios, EquatableArray<RuleArgument>.Empty,
                    CustomMethodName: methodName,
                    CustomDeclaringType: declaringType);
            }

            default:
                return null;
        }
    }

    private static bool RequireString(
        AttributeData attribute,
        PropertyShape shape,
        string ruleName,
        List<DiagnosticInfo> diagnostics)
    {
        if (shape.IsString)
        {
            return true;
        }

        diagnostics.Add(DiagnosticInfo.Create(
            ValidatorEmitter.UnsupportedTargetId,
            $"[{ruleName}] only applies to string properties; '{shape.Name}' is '{shape.BareTypeName}'.",
            GetAttributeLocation(attribute)));
        return false;
    }

    private static bool RequireNonString(
        AttributeData attribute,
        PropertyShape shape,
        string ruleName,
        List<DiagnosticInfo> diagnostics)
    {
        if (!shape.IsString)
        {
            return true;
        }

        diagnostics.Add(DiagnosticInfo.Create(
            ValidatorEmitter.UnsupportedTargetId,
            $"[{ruleName}] applies to comparable value properties; '{shape.Name}' is a string.",
            GetAttributeLocation(attribute)));
        return false;
    }

    private static Location? GetAttributeLocation(AttributeData attribute) =>
        attribute.ApplicationSyntaxReference is { } reference
            ? Location.Create(reference.SyntaxTree, reference.Span)
            : null;

    private static EquatableArray<RuleArgument> Arguments(params RuleArgument[] arguments) => new(arguments);

    private static object? GetConstructorValue(AttributeData attribute, int index) =>
        attribute.ConstructorArguments.Length > index ? attribute.ConstructorArguments[index].Value : null;

    private static int GetConstructorInt(AttributeData attribute, int index) =>
        GetConstructorValue(attribute, index) is int value ? value : 0;

    /// <summary>
    /// Renders a numeric threshold as a C# literal. Uses the invariant culture (the host locale
    /// must never leak into generated code) and picks a literal suffix from the property type so
    /// comparisons against decimal/float properties compile.
    /// </summary>
    private static string RenderNumericLiteral(object? value, string bareTypeName)
    {
        var text = Convert.ToString(value, CultureInfo.InvariantCulture) ?? "0";
        var suffix = bareTypeName switch
        {
            "decimal" => "m",
            "double" => "d",
            "float" => "f",
            "long" => "L",
            "ulong" => "UL",
            "uint" => "u",
            _ => string.Empty,
        };

        return text + suffix;
    }

    private static bool IsAccessible(INamedTypeSymbol type)
    {
        for (INamedTypeSymbol? current = type; current is not null; current = current.ContainingType)
        {
            if (current.DeclaredAccessibility is not (Accessibility.Public or Accessibility.Internal))
            {
                return false;
            }
        }

        return true;
    }
}
