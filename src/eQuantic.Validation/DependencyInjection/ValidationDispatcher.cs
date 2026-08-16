using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace eQuantic.Validation;

/// <summary>Default dispatcher used by the HTTP integrations and available for message consumers.</summary>
public sealed class ValidationDispatcher : IValidationDispatcher
{
    private readonly IReadOnlyList<ValidationRegistration> _registrations;

    /// <summary>Initializes the dispatcher from registered validator descriptors.</summary>
    internal ValidationDispatcher(IEnumerable<ValidationRegistration> registrations)
    {
        _registrations = registrations?.ToArray() ?? throw new ArgumentNullException(nameof(registrations));
    }

    /// <inheritdoc />
    public async Task<ValidationResult> ValidateAsync(
        object model,
        IServiceProvider services,
        ValidationContext? context = null,
        CancellationToken cancellationToken = default)
    {
        if (model is null)
        {
            throw new ArgumentNullException(nameof(model));
        }

        if (services is null)
        {
            throw new ArgumentNullException(nameof(services));
        }

        var messageProvider = context?.MessageProvider ?? services.GetService<IValidationMessageProvider>();
        var effectiveContext = context is not null
            ? (context.MessageProvider is null && messageProvider is not null
                ? new ValidationContext(
                    scenarios: context.Scenarios,
                    includedPaths: context.IncludedPaths,
                    services: context.Services ?? services,
                    executionMode: context.ExecutionMode,
                    messageProvider: messageProvider)
                : context)
            : new ValidationContext(services: services, messageProvider: messageProvider);

        var modelType = model.GetType();
        var modelName = modelType.Name;

        var validators = _registrations
            .Where(registration => registration.ModelType == modelType)
            .Select(registration => registration.Resolve(services))
            .ToArray();

        if (validators.Length == 0)
        {
            return ValidationResult.Success;
        }

        using var activity = ValidationDiagnostics.ActivitySource.StartActivity("Validation.Execute");
        activity?.SetTag("validation.model", modelName);

        var startTimestamp = Stopwatch.GetTimestamp();

        ValidationResult[] results;
        if (effectiveContext.ExecutionMode == ValidationExecutionMode.Parallel)
        {
            var tasks = validators
                .Select(validator => validator.ValidateAsync(model, effectiveContext, cancellationToken))
                .ToArray();
            results = await Task.WhenAll(tasks).ConfigureAwait(false);
        }
        else
        {
            results = new ValidationResult[validators.Length];
            for (var index = 0; index < validators.Length; index++)
            {
                results[index] = await validators[index]
                    .ValidateAsync(model, effectiveContext, cancellationToken)
                    .ConfigureAwait(false);
            }
        }

        var combinedResult = ValidationResult.Combine(results);
        var durationSeconds = GetElapsedSeconds(startTimestamp);

        ValidationDiagnostics.ValidationDuration.Record(durationSeconds, new KeyValuePair<string, object?>("model", modelName));

        var status = combinedResult.IsValid ? "success" : "failed";
        ValidationDiagnostics.ValidationsTotal.Add(1,
            new KeyValuePair<string, object?>("model", modelName),
            new KeyValuePair<string, object?>("status", status));

        if (!combinedResult.IsValid)
        {
            foreach (var failure in combinedResult.Failures)
            {
                ValidationDiagnostics.FailuresTotal.Add(1,
                    new KeyValuePair<string, object?>("model", modelName),
                    new KeyValuePair<string, object?>("code", failure.Code),
                    new KeyValuePair<string, object?>("severity", failure.Severity.ToString()));
            }
        }

        return combinedResult;
    }

    private static double GetElapsedSeconds(long startTimestamp)
    {
#if NET8_0_OR_GREATER
        return Stopwatch.GetElapsedTime(startTimestamp).TotalSeconds;
#else
        return (Stopwatch.GetTimestamp() - startTimestamp) / (double)Stopwatch.Frequency;
#endif
    }
}
