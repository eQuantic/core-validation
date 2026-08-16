using System.Collections.ObjectModel;

namespace eQuantic.Validation;

/// <summary>
/// Supplies execution scope and application services to a validation operation.
/// A context is immutable and may be reused safely.
/// </summary>
public sealed class ValidationContext
{
    private static readonly IReadOnlyCollection<string> EmptySet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    private static readonly IReadOnlyDictionary<string, object?> EmptyItems =
        new ReadOnlyDictionary<string, object?>(new Dictionary<string, object?>());

    /// <summary>Creates an unscoped validation context.</summary>
    public ValidationContext(
        IEnumerable<string>? scenarios = null,
        IEnumerable<string>? includedPaths = null,
        IServiceProvider? services = null,
        IReadOnlyDictionary<string, object?>? items = null,
        IValidationMessageProvider? messageProvider = null,
        ValidationExecutionMode executionMode = ValidationExecutionMode.Sequential)
    {
        Scenarios = ToSet(scenarios);
        IncludedPaths = ToSet(includedPaths);
        Services = services;
        Items = items is null
            ? EmptyItems
            : new ReadOnlyDictionary<string, object?>(items.ToDictionary(
                static item => item.Key,
                static item => item.Value,
                StringComparer.Ordinal));
        MessageProvider = messageProvider;
        ExecutionMode = executionMode;
    }

    /// <summary>Gets a context that executes every rule.</summary>
    public static ValidationContext Default { get; } = new();

    /// <summary>Gets the named scenarios requested by the caller.</summary>
    public IReadOnlyCollection<string> Scenarios { get; }

    /// <summary>Gets the member paths requested by the caller for partial validation.</summary>
    public IReadOnlyCollection<string> IncludedPaths { get; }

    /// <summary>Gets optional application services available to rule predicates.</summary>
    public IServiceProvider? Services { get; }

    /// <summary>Gets caller-supplied contextual values.</summary>
    public IReadOnlyDictionary<string, object?> Items { get; }

    /// <summary>Gets an optional resolver for localized validation messages.</summary>
    public IValidationMessageProvider? MessageProvider { get; }

    /// <summary>Gets the scheduling mode for independent rules.</summary>
    public ValidationExecutionMode ExecutionMode { get; }

    /// <summary>Creates a context for one or more named scenarios.</summary>
    public static ValidationContext ForScenarios(params string[] scenarios) => new(scenarios: scenarios);

    /// <summary>Creates a context that validates only the requested members and their parents or children.</summary>
    public static ValidationContext ForPaths(params string[] paths) => new(includedPaths: paths);

    /// <summary>
    /// Creates the context to use while validating a nested member. Selected paths are made
    /// relative to <paramref name="parentPath"/>, so a root selection of
    /// <c>Address.PostalCode</c> becomes <c>PostalCode</c> for an address validator.
    /// </summary>
    public ValidationContext CreateChildScope(string parentPath)
    {
        if (string.IsNullOrWhiteSpace(parentPath))
        {
            throw new ArgumentException("A parent path is required.", nameof(parentPath));
        }

        if (IncludedPaths.Count == 0)
        {
            return this;
        }

        var childPaths = new List<string>();
        foreach (var selectedPath in IncludedPaths)
        {
            if (string.Equals(selectedPath, parentPath, StringComparison.OrdinalIgnoreCase) ||
                IsDescendant(parentPath, selectedPath))
            {
                return new ValidationContext(
                    scenarios: Scenarios,
                    services: Services,
                    items: Items,
                    messageProvider: MessageProvider,
                    executionMode: ExecutionMode);
            }

            if (IsDescendant(selectedPath, parentPath))
            {
                childPaths.Add(selectedPath.Substring(parentPath.Length + 1));
            }
        }

        return new ValidationContext(
            scenarios: Scenarios,
            includedPaths: childPaths.Count == 0 ? new[] { "\u0000" } : childPaths,
            services: Services,
            items: Items,
            messageProvider: MessageProvider,
            executionMode: ExecutionMode);
    }

    /// <summary>Returns a service from <see cref="Services"/> or <see langword="null"/> when unavailable.</summary>
    public TService? GetService<TService>()
        where TService : class => Services?.GetService(typeof(TService)) as TService;

    /// <summary>Returns a service from <see cref="Services"/> or <see langword="null"/> when unavailable.</summary>
    public object? GetService(Type serviceType) => Services?.GetService(serviceType);

    /// <summary>Gets whether a rule path belongs to the selected partial-validation scope.</summary>
    public bool IncludesPath(string path)
    {
        if (IncludedPaths.Count == 0)
        {
            return true;
        }

        foreach (var selectedPath in IncludedPaths)
        {
            if (string.Equals(path, selectedPath, StringComparison.OrdinalIgnoreCase) ||
                IsDescendant(path, selectedPath) ||
                IsDescendant(selectedPath, path))
            {
                return true;
            }
        }

        return false;
    }

    private static IReadOnlyCollection<string> ToSet(IEnumerable<string>? values)
    {
        if (values is null)
        {
            return EmptySet;
        }

        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var value in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                set.Add(value.Trim());
            }
        }

        return set.Count == 0
            ? EmptySet
            : new ReadOnlyCollection<string>(set.OrderBy(static value => value, StringComparer.OrdinalIgnoreCase).ToArray());
    }

    private static bool IsDescendant(string path, string possibleParent)
    {
        return path.StartsWith(possibleParent + ".", StringComparison.OrdinalIgnoreCase) ||
               path.StartsWith(possibleParent + "[", StringComparison.OrdinalIgnoreCase);
    }
}
