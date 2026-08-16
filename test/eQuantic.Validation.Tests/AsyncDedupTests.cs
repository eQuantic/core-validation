using Microsoft.Extensions.DependencyInjection;

namespace eQuantic.Validation.Tests;

[TestFixture]
public sealed class AsyncDedupTests
{
    [Test]
    public async Task Duplicate_collection_elements_evaluate_the_async_rule_once_per_value()
    {
        var evaluations = 0;
        var validator = new InlineValidator<Batch>();
        validator.RuleForEach(x => x.Emails).MustAsync((_, _, _, _) =>
        {
            Interlocked.Increment(ref evaluations);
            return Task.FromResult(true);
        });

        var batch = new Batch(new[] { "a@x.com", "a@x.com", "b@x.com" });

        await validator.ValidateAsync(batch, new ValidationContext(deduplicateAsyncRules: true));

        Assert.That(evaluations, Is.EqualTo(2), "equal values must reuse the first evaluation");
    }

    [Test]
    public async Task Without_the_cache_every_evaluation_runs_as_before()
    {
        var evaluations = 0;
        var validator = new InlineValidator<Batch>();
        validator.RuleForEach(x => x.Emails).MustAsync((_, _, _, _) =>
        {
            Interlocked.Increment(ref evaluations);
            return Task.FromResult(true);
        });

        var batch = new Batch(new[] { "a@x.com", "a@x.com", "b@x.com" });

        await validator.ValidateAsync(batch);

        Assert.That(evaluations, Is.EqualTo(3));
    }

    [Test]
    public async Task Revalidating_within_the_same_context_reuses_results_including_nested_validators()
    {
        var rootEvaluations = 0;
        var nestedEvaluations = 0;

        var nested = new InlineValidator<Contact>(v =>
            v.RuleFor(x => x.Email).MustAsync((_, _, _, _) =>
            {
                Interlocked.Increment(ref nestedEvaluations);
                return Task.FromResult(true);
            }));

        var validator = new InlineValidator<Batch>(v =>
        {
            v.RuleFor(x => x.Owner).MustAsync((_, _, _, _) =>
            {
                Interlocked.Increment(ref rootEvaluations);
                return Task.FromResult(true);
            });
            v.RuleFor(x => x.Contact!).SetValidator(nested);
        });

        var batch = new Batch(new[] { "a@x.com" }, "owner", new Contact("c@x.com"));
        var context = new ValidationContext(deduplicateAsyncRules: true);

        await validator.ValidateAsync(batch, context);
        await validator.ValidateAsync(batch, context);

        Assert.Multiple(() =>
        {
            Assert.That(rootEvaluations, Is.EqualTo(1));
            Assert.That(nestedEvaluations, Is.EqualTo(1), "child scopes must share the operation cache");
        });
    }

    [Test]
    public async Task Dispatcher_deduplicates_shared_composed_rules_automatically()
    {
        SharedOwnerValidator.Evaluations = 0;

        var services = new ServiceCollection();
        services.AddValidator<Batch, FirstComposedValidator>();
        services.AddValidator<Batch, SecondComposedValidator>();

        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var dispatcher = scope.ServiceProvider.GetRequiredService<IValidationDispatcher>();

        var result = await dispatcher.ValidateAsync(
            new Batch(new[] { "a@x.com" }, "owner"),
            scope.ServiceProvider);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsValid, Is.True);
            Assert.That(SharedOwnerValidator.Evaluations, Is.EqualTo(1),
                "the same composed rule must run once per dispatcher operation");
        });
    }

    private sealed record Batch(IReadOnlyList<string> Emails, string? Owner = null, Contact? Contact = null);
    private sealed record Contact(string? Email);

    private sealed class SharedOwnerValidator : Validator<Batch>
    {
        public static int Evaluations;

        public static readonly SharedOwnerValidator Instance = new();

        public SharedOwnerValidator()
        {
            RuleFor(x => x.Owner).MustAsync((_, _, _, _) =>
            {
                Interlocked.Increment(ref Evaluations);
                return Task.FromResult(true);
            });
        }
    }

    private sealed class FirstComposedValidator : Validator<Batch>
    {
        public FirstComposedValidator() => Include(SharedOwnerValidator.Instance);
    }

    private sealed class SecondComposedValidator : Validator<Batch>
    {
        public SecondComposedValidator() => Include(SharedOwnerValidator.Instance);
    }
}
