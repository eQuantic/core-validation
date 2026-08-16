namespace eQuantic.Validation.Internal;

internal sealed class PropertyRule<T, TProperty> : IValidationRule<T>
{
    private readonly Func<T, TProperty> _accessor;
    private readonly List<ValidationCheck<T, TProperty>> _checks = new();
    private readonly HashSet<string> _scenarios = new(StringComparer.OrdinalIgnoreCase);
    private Func<T, ValidationContext, bool>? _condition;
    private IValidator<TProperty>? _childValidator;

    public PropertyRule(string path, Func<T, TProperty> accessor)
    {
        Path = path;
        DisplayName = string.IsNullOrEmpty(path) ? typeof(T).Name : path.Split('.').Last();
        _accessor = accessor;
    }

    public string Path { get; }

    public string DisplayName { get; set; }

    public bool StopOnFirstFailure { get; set; }

    public ValidationCheck<T, TProperty>? LastCheck => _checks.Count == 0 ? null : _checks[_checks.Count - 1];

    public bool IsAsync => _checks.Any(static check => check.IsAsync) || _childValidator is not null;

    public void Add(ValidationCheck<T, TProperty> check) => _checks.Add(check);

    public void AddScenarios(IEnumerable<string> scenarios)
    {
        foreach (var scenario in scenarios)
        {
            if (!string.IsNullOrWhiteSpace(scenario))
            {
                _scenarios.Add(scenario.Trim());
            }
        }
    }

    public void SetCondition(Func<T, ValidationContext, bool> condition) =>
        _condition = condition ?? throw new ArgumentNullException(nameof(condition));

    public void SetChildValidator(IValidator<TProperty> validator) =>
        _childValidator = validator ?? throw new ArgumentNullException(nameof(validator));

    public bool AppliesTo(ValidationContext context)
    {
        if (!context.IncludesPath(Path))
        {
            return false;
        }

        return _scenarios.Count == 0 || _scenarios.Overlaps(context.Scenarios);
    }

    public IEnumerable<ValidationRuleDescriptor> Describe()
    {
        var scenarios = _scenarios.Count == 0 ? null : _scenarios.ToArray();
        var conditional = _condition is not null;

        foreach (var check in _checks)
        {
            yield return new ValidationRuleDescriptor(
                check.TargetPath ?? Path,
                check.Kind,
                check.Code,
                check.Template,
                check.Severity,
                check.Arguments,
                scenarios,
                check.IsAsync,
                conditional,
                valueType: typeof(TProperty));
        }

        if (_childValidator is IDescribableValidator describable)
        {
            foreach (var rule in describable.Describe().Rules)
            {
                yield return Path.Length == 0 ? rule : rule.WithPathPrefix(Path, conditional);
            }
        }
        else if (_childValidator is not null)
        {
            yield return new ValidationRuleDescriptor(
                Path,
                ValidationRuleKinds.Nested,
                ValidationRuleKinds.Nested,
                "{Property} is invalid.",
                ValidationSeverity.Error,
                arguments: null,
                scenarios,
                isAsync: false,
                conditional);
        }
    }

    public IReadOnlyList<ValidationFailure> Validate(T instance, ValidationContext context)
    {
        if (!AppliesToInstance(instance, context))
        {
            return Array.Empty<ValidationFailure>();
        }

        var propertyValue = _accessor(instance);
        var failures = new List<ValidationFailure>();

        foreach (var check in _checks)
        {
            if (check.IsAsync)
            {
                throw new AsyncValidationRequiredException(typeof(T));
            }

            if (!check.Passes(instance, propertyValue))
            {
                failures.Add(check.CreateFailure(context, Path, DisplayName));
                if (StopOnFirstFailure)
                {
                    return failures;
                }
            }
        }

        if (_childValidator is not null && propertyValue is not null)
        {
            var childResult = _childValidator.Validate(propertyValue, context.CreateChildScope(Path));
            failures.AddRange(childResult.Failures.Select(failure => failure.WithPathPrefix(Path)));
        }

        return failures;
    }

    public async Task<IReadOnlyList<ValidationFailure>> ValidateAsync(
        T instance,
        ValidationContext context,
        CancellationToken cancellationToken)
    {
        if (!AppliesToInstance(instance, context))
        {
            return Array.Empty<ValidationFailure>();
        }

        var propertyValue = _accessor(instance);
        var failures = new List<ValidationFailure>();

        foreach (var check in _checks)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!await check.PassesAsync(instance, propertyValue, context, cancellationToken).ConfigureAwait(false))
            {
                failures.Add(check.CreateFailure(context, Path, DisplayName));
                if (StopOnFirstFailure)
                {
                    return failures;
                }
            }
        }

        if (_childValidator is not null && propertyValue is not null)
        {
            var childResult = await _childValidator
                .ValidateAsync(propertyValue, context.CreateChildScope(Path), cancellationToken)
                .ConfigureAwait(false);
            failures.AddRange(childResult.Failures.Select(failure => failure.WithPathPrefix(Path)));
        }

        return failures;
    }

    private bool AppliesToInstance(T instance, ValidationContext context)
    {
        if (!context.IncludesPath(Path))
        {
            return false;
        }

        if (_scenarios.Count > 0 && !_scenarios.Overlaps(context.Scenarios))
        {
            return false;
        }

        return _condition?.Invoke(instance, context) ?? true;
    }
}
