using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace eQuantic.Validation;

/// <summary>
/// OpenTelemetry metrics and tracing instrumentation for eQuantic.Validation.
/// </summary>
public static class ValidationDiagnostics
{
    /// <summary>Meter name used for OpenTelemetry metrics export.</summary>
    public const string MeterName = "eQuantic.Validation";

    /// <summary>ActivitySource name used for OpenTelemetry distributed tracing.</summary>
    public const string ActivitySourceName = "eQuantic.Validation";

    private static readonly string Version =
        typeof(ValidationDiagnostics).Assembly.GetName().Version?.ToString(3) ?? "0.0.0";

    /// <summary>Meter instance for recording validation metrics.</summary>
    public static readonly Meter Meter = new(MeterName, Version);

    /// <summary>ActivitySource instance for distributed tracing.</summary>
    public static readonly ActivitySource ActivitySource = new(ActivitySourceName, Version);

    /// <summary>Total number of validation requests executed.</summary>
    public static readonly Counter<long> ValidationsTotal = Meter.CreateCounter<long>(
        "validation.requests.total",
        "{request}",
        "Total number of validation requests executed.");

    /// <summary>Total number of validation failures broken down by error code.</summary>
    public static readonly Counter<long> FailuresTotal = Meter.CreateCounter<long>(
        "validation.failures.total",
        "{failure}",
        "Total number of validation failures by error code.");

    /// <summary>Histogram tracking validation execution duration in seconds (OTel convention).</summary>
    public static readonly Histogram<double> ValidationDuration = Meter.CreateHistogram<double>(
        "validation.duration",
        "s",
        "Duration of validation execution in seconds.");
}
