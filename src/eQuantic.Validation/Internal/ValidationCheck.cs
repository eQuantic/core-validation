namespace eQuantic.Validation.Internal;

internal sealed class ValidationCheck<T, TProperty>
{
    public ValidationCheck(
        Func<T, TProperty, bool>? predicate,
        Func<T, TProperty, ValidationContext, CancellationToken, Task<bool>>? asyncPredicate,
        string code,
        string template,
        IReadOnlyDictionary<string, object?>? arguments = null,
        string? targetPath = null)
    {
        Predicate = predicate;
        AsyncPredicate = asyncPredicate;
        Code = code;
        Template = template;
        Arguments = arguments ?? new Dictionary<string, object?>();
        TargetPath = targetPath;
    }

    public Func<T, TProperty, bool>? Predicate { get; }

    public Func<T, TProperty, ValidationContext, CancellationToken, Task<bool>>? AsyncPredicate { get; }

    public string Code { get; set; }

    public string Template { get; set; }

    public string Kind { get; set; } = ValidationRuleKinds.Predicate;

    public ValidationSeverity Severity { get; set; } = ValidationSeverity.Error;

    public IReadOnlyDictionary<string, object?> Arguments { get; }

    public string? TargetPath { get; set; }

    public bool IsAsync => AsyncPredicate is not null;

    public bool Passes(T instance, TProperty propertyValue)
    {
        return Predicate?.Invoke(instance, propertyValue) ?? throw new InvalidOperationException("The check is asynchronous.");
    }

    public Task<bool> PassesAsync(
        T instance,
        TProperty propertyValue,
        ValidationContext context,
        CancellationToken cancellationToken)
    {
        if (AsyncPredicate is not null)
        {
            return AsyncPredicate(instance, propertyValue, context, cancellationToken);
        }

        return Task.FromResult(Passes(instance, propertyValue));
    }

    public ValidationFailure CreateFailure(ValidationContext context, string path, string displayName)
    {
        var effectivePath = TargetPath ?? path;
        var effectiveName = string.IsNullOrEmpty(effectivePath)
            ? displayName
            : effectivePath.Split('.').Last();

        var message = ValidationMessages.Format(context, effectivePath, effectiveName, Code, Template, Arguments);
        return new ValidationFailure(effectivePath, Code, message, Severity, Arguments);
    }
}
