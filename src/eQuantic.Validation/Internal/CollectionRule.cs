namespace eQuantic.Validation.Internal;

internal sealed class CollectionRule<T, TElement> : IValidationRule<T>
{
    private readonly Func<T, IEnumerable<TElement>?> _accessor;
    private readonly List<ValidationCheck<T, TElement>> _checks = new();
    private readonly HashSet<string> _scenarios = new(StringComparer.OrdinalIgnoreCase);
    private Func<T, ValidationContext, bool>? _condition;
    private IValidator<TElement>? _childValidator;

    public CollectionRule(string path, Func<T, IEnumerable<TElement>?> accessor)
    {
        Path = path;
        _accessor = accessor;
    }

    public string Path { get; }

    public ValidationCheck<T, TElement>? LastCheck => _checks.Count == 0 ? null : _checks[_checks.Count - 1];

    public bool IsAsync => _checks.Any(static check => check.IsAsync) || _childValidator is not null;

    public void Add(ValidationCheck<T, TElement> check) => _checks.Add(check);

    public void AddScenarios(IEnumerable<string> scenarios)
    {
        foreach (var scenario in scenarios.Where(static scenario => !string.IsNullOrWhiteSpace(scenario)))
        {
            _scenarios.Add(scenario.Trim());
        }
    }

    public void SetChildValidator(IValidator<TElement> validator) =>
        _childValidator = validator ?? throw new ArgumentNullException(nameof(validator));

    public void SetCondition(Func<T, ValidationContext, bool> condition) =>
        _condition = condition ?? throw new ArgumentNullException(nameof(condition));

    public bool AppliesTo(ValidationContext context) =>
        context.IncludesPath(Path) &&
        (_scenarios.Count == 0 || _scenarios.Overlaps(context.Scenarios));

    private bool AppliesToInstance(T instance, ValidationContext context) =>
        AppliesTo(context) && (_condition?.Invoke(instance, context) ?? true);

    public IReadOnlyList<ValidationFailure> Validate(T instance, ValidationContext context)
    {
        if (!AppliesToInstance(instance, context))
        {
            return Array.Empty<ValidationFailure>();
        }

        var failures = new List<ValidationFailure>();
        var elements = _accessor(instance);
        if (elements is null)
        {
            return failures;
        }

        var index = 0;
        foreach (var element in elements)
        {
            var elementPath = Path + "[" + index + "]";
            if (!context.IncludesPath(elementPath))
            {
                index++;
                continue;
            }

            foreach (var check in _checks)
            {
                if (check.IsAsync)
                {
                    throw new AsyncValidationRequiredException(typeof(T));
                }

                if (!check.Passes(instance, element))
                {
                    failures.Add(check.CreateFailure(context, elementPath, elementPath));
                }
            }

            if (_childValidator is not null && element is not null)
            {
                failures.AddRange(_childValidator.Validate(element, context.CreateChildScope(elementPath)).Failures
                    .Select(failure => failure.WithPathPrefix(elementPath)));
            }

            index++;
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

        var failures = new List<ValidationFailure>();
        var elements = _accessor(instance);
        if (elements is null)
        {
            return failures;
        }

        var index = 0;
        foreach (var element in elements)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var elementPath = Path + "[" + index + "]";
            if (!context.IncludesPath(elementPath))
            {
                index++;
                continue;
            }

            foreach (var check in _checks)
            {
                if (!await check.PassesAsync(instance, element, context, cancellationToken).ConfigureAwait(false))
                {
                    failures.Add(check.CreateFailure(context, elementPath, elementPath));
                }
            }

            if (_childValidator is not null && element is not null)
            {
                var childResult = await _childValidator
                    .ValidateAsync(element, context.CreateChildScope(elementPath), cancellationToken)
                    .ConfigureAwait(false);
                failures.AddRange(childResult.Failures.Select(failure => failure.WithPathPrefix(elementPath)));
            }

            index++;
        }

        return failures;
    }
}
