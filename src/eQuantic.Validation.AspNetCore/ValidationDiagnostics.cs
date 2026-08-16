using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace eQuantic.Validation.AspNetCore;

/// <summary>
/// OpenTelemetry Metrics and Tracing diagnostics for eQuantic.Validation.
/// </summary>
public static class ValidationDiagnostics
{
    /// <summary>Meter name used for OpenTelemetry metrics export.</summary>
    public const string MeterName = "eQuantic.Validation";

    /// <summary>ActivitySource name used for OpenTelemetry distributed tracing.</summary>
    public const string ActivitySourceName = "eQuantic.Validation";

    /// <summary>Instrumentation version.</summary>
    public const string Version = "1.0.0";

    /// <summary>Meter instance for recording validation metrics.</summary>
    public static readonly Meter Meter = new(MeterName, Version);

    /// <summary>ActivitySource instance for distributed tracing.</summary>
    public static readonly ActivitySource ActivitySource = new(ActivitySourceName, Version);

    /// <summary>Total number of validation requests executed.</summary>
    public static readonly Counter<long> ValidationsTotal = Meter.CreateCounter<long>(
        "validation.requests.total",
        "count",
        "Total number of validation requests executed.");

    /// <summary>Total number of validation failures broken down by error code.</summary>
    public static readonly Counter<long> FailuresTotal = Meter.CreateCounter<long>(
        "validation.failures.total",
        "count",
        "Total number of validation failures by error code.");

    /// <summary>Histogram tracking validation execution duration in milliseconds.</summary>
    public static readonly Histogram<double> ValidationDuration = Meter.CreateHistogram<double>(
        "validation.duration.ms",
        "ms",
        "Duration of validation execution in milliseconds.");
}
