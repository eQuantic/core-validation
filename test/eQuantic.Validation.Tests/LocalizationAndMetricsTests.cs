using System.Diagnostics.Metrics;
using eQuantic.Validation.AspNetCore;
using eQuantic.Validation.Attributes;
using eQuantic.Validation.Generated;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;

namespace eQuantic.Validation.Tests;

[TestFixture]
public sealed class LocalizationAndMetricsTests
{
    [Test]
    public async Task StringLocalizerValidationMessageProvider_resolves_message_by_code()
    {
        var localizer = new FakeStringLocalizer(new Dictionary<string, string>
        {
            ["custom.email.invalid"] = "O endereço de e-mail informado é inválido.",
            ["The Title field is required."] = "O campo Título é obrigatório."
        });

        var provider = new StringLocalizerValidationMessageProvider(localizer);

        var descriptorWithCode = new ValidationMessageDescriptor(
            "Email",
            "Email",
            "custom.email.invalid",
            "{Property} is invalid.",
            new Dictionary<string, object?>());

        var resolvedCode = provider.Resolve(descriptorWithCode);
        Assert.That(resolvedCode, Is.EqualTo("O endereço de e-mail informado é inválido."));

        var descriptorWithTemplate = new ValidationMessageDescriptor(
            "Title",
            "Title",
            "required",
            "The Title field is required.",
            new Dictionary<string, object?>());

        var resolvedTemplate = provider.Resolve(descriptorWithTemplate);
        Assert.That(resolvedTemplate, Is.EqualTo("O campo Título é obrigatório."));
    }

    [Test]
    public async Task ValidationDispatcher_records_metrics_and_applies_localization()
    {
        var localizer = new FakeStringLocalizer(new Dictionary<string, string>
        {
            ["custom.email.invalid"] = "Email inválido para o usuário."
        });

        var services = new ServiceCollection();
        services.AddGeneratedValidation();
        services.AddValidationLocalization(localizer);

        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var dispatcher = scope.ServiceProvider.GetRequiredService<IValidationDispatcher>();

        var user = new SourceGenUser("Valid Name", "invalid-email", 20);
        var result = await dispatcher.ValidateAsync(user, scope.ServiceProvider);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsValid, Is.False);
            Assert.That(result.Errors.Single(e => e.Code == "custom.email.invalid").Message, Is.EqualTo("Email inválido para o usuário."));
        });
    }

    private sealed class FakeStringLocalizer : IStringLocalizer
    {
        private readonly IReadOnlyDictionary<string, string> _translations;

        public FakeStringLocalizer(IReadOnlyDictionary<string, string> translations)
        {
            _translations = translations;
        }

        public LocalizedString this[string name]
        {
            get
            {
                if (_translations.TryGetValue(name, out var value))
                {
                    return new LocalizedString(name, value, resourceNotFound: false);
                }

                return new LocalizedString(name, name, resourceNotFound: true);
            }
        }

        public LocalizedString this[string name, params object[] arguments] =>
            this[name];

        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) =>
            _translations.Select(t => new LocalizedString(t.Key, t.Value, resourceNotFound: false));
    }
}
