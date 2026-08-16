# Observability

The dispatcher (used by `RequireValidation`, the MVC filter and any direct `IValidationDispatcher`
call) emits OpenTelemetry-compatible metrics and traces with **zero PII** — model names, codes and
severities only, never values.

## Instruments

Meter and activity source are both named `eQuantic.Validation`:

| Instrument | Type | Unit | Tags |
| --- | --- | --- | --- |
| `validation.requests.total` | counter | `{request}` | `model`, `status` (`success`/`failed`) |
| `validation.failures.total` | counter | `{failure}` | `model`, `code`, `severity` |
| `validation.duration` | histogram | `s` (OTel convention) | `model` |

Each dispatched validation also runs inside a `Validation.Execute` activity tagged with
`validation.model`, so validation time shows up inside your request traces.

## Wiring

```csharp
builder.Services.AddOpenTelemetry()
    .WithMetrics(metrics => metrics
        .AddMeter(eQuantic.Validation.ValidationDiagnostics.MeterName)
        .AddOtlpExporter())
    .WithTracing(tracing => tracing
        .AddSource(eQuantic.Validation.ValidationDiagnostics.ActivitySourceName)
        .AddOtlpExporter());
```

## What to alert on

- `validation.failures.total` by `code` — a spike on one code usually means a client shipped a
  bug (or an attack probe); stable codes make the dashboards survive message rewording.
- `validation.duration` p99 — async rules hitting slow dependencies show up here before they
  hurt request latency dashboards.
- `severity="Warning"` volume — measures how much traffic still relies on soft-deprecated inputs
  (pairs with the [`Validation-Warnings` response header](aspnetcore.md)).
