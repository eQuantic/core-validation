using System.Globalization;

namespace eQuantic.Validation;

/// <summary>
/// Built-in translations for the library's default message templates, resolved per call from
/// <see cref="CultureInfo.CurrentUICulture"/> (combine with request localization middleware so
/// the <c>Accept-Language</c> header drives the language). Custom messages set with
/// <c>WithMessage</c> are never touched: only the stock English templates are translated, and
/// unsupported languages fall back to English. Shipping languages: en, pt, es, fr, de, it.
/// </summary>
public sealed class BuiltInValidationMessages : IValidationMessageProvider
{
    /// <summary>Gets the shared provider instance.</summary>
    public static BuiltInValidationMessages Instance { get; } = new();

    private BuiltInValidationMessages()
    {
    }

    /// <inheritdoc />
    public string Resolve(ValidationMessageDescriptor descriptor)
    {
        if (descriptor is null)
        {
            throw new ArgumentNullException(nameof(descriptor));
        }

        var language = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
        return Translations.TryGetValue(language, out var templates) &&
               templates.TryGetValue(descriptor.Template, out var translated)
            ? translated
            : descriptor.Template;
    }

    private static readonly Dictionary<string, Dictionary<string, string>> Translations = new(StringComparer.OrdinalIgnoreCase)
    {
        ["pt"] = new(StringComparer.Ordinal)
        {
            ["{Property} is required."] = "{Property} é obrigatório.",
            ["{Property} must not be empty."] = "{Property} não pode ser vazio.",
            ["{Property} must not be blank."] = "{Property} não pode estar em branco.",
            ["{Property} must be a valid email address."] = "{Property} deve ser um endereço de e-mail válido.",
            ["{Property} must contain at least {MinimumLength} characters."] = "{Property} deve conter pelo menos {MinimumLength} caracteres.",
            ["{Property} must contain no more than {MaximumLength} characters."] = "{Property} deve conter no máximo {MaximumLength} caracteres.",
            ["{Property} must contain between {MinimumLength} and {MaximumLength} characters."] = "{Property} deve conter entre {MinimumLength} e {MaximumLength} caracteres.",
            ["{Property} has an invalid format."] = "{Property} tem um formato inválido.",
            ["{Property} has an unexpected value."] = "{Property} tem um valor inesperado.",
            ["{Property} has a forbidden value."] = "{Property} tem um valor não permitido.",
            ["{Property} must be one of the allowed values."] = "{Property} deve ser um dos valores permitidos.",
            ["{Property} must be greater than {Minimum}."] = "{Property} deve ser maior que {Minimum}.",
            ["{Property} must be at least {Minimum}."] = "{Property} deve ser no mínimo {Minimum}.",
            ["{Property} must be less than {Maximum}."] = "{Property} deve ser menor que {Maximum}.",
            ["{Property} must be at most {Maximum}."] = "{Property} deve ser no máximo {Maximum}.",
            ["{Property} must be between {Minimum} and {Maximum}."] = "{Property} deve estar entre {Minimum} e {Maximum}.",
            ["{Property} is invalid."] = "{Property} é inválido.",
            ["The instance is required."] = "A instância é obrigatória.",
            ["The request body is required."] = "O corpo da requisição é obrigatório.",
            ["{Property} must be greater than {OtherProperty}."] = "{Property} deve ser maior que {OtherProperty}.",
            ["{Property} must be at least {OtherProperty}."] = "{Property} deve ser no mínimo {OtherProperty}.",
            ["{Property} must be less than {OtherProperty}."] = "{Property} deve ser menor que {OtherProperty}.",
            ["{Property} must be at most {OtherProperty}."] = "{Property} deve ser no máximo {OtherProperty}.",
            ["{Property} must be equal to {OtherProperty}."] = "{Property} deve ser igual a {OtherProperty}.",
            ["{Property} must differ from {OtherProperty}."] = "{Property} deve ser diferente de {OtherProperty}.",
        },
        ["es"] = new(StringComparer.Ordinal)
        {
            ["{Property} is required."] = "{Property} es obligatorio.",
            ["{Property} must not be empty."] = "{Property} no puede estar vacío.",
            ["{Property} must not be blank."] = "{Property} no puede estar en blanco.",
            ["{Property} must be a valid email address."] = "{Property} debe ser una dirección de correo electrónico válida.",
            ["{Property} must contain at least {MinimumLength} characters."] = "{Property} debe contener al menos {MinimumLength} caracteres.",
            ["{Property} must contain no more than {MaximumLength} characters."] = "{Property} debe contener como máximo {MaximumLength} caracteres.",
            ["{Property} must contain between {MinimumLength} and {MaximumLength} characters."] = "{Property} debe contener entre {MinimumLength} y {MaximumLength} caracteres.",
            ["{Property} has an invalid format."] = "{Property} tiene un formato no válido.",
            ["{Property} has an unexpected value."] = "{Property} tiene un valor inesperado.",
            ["{Property} has a forbidden value."] = "{Property} tiene un valor no permitido.",
            ["{Property} must be one of the allowed values."] = "{Property} debe ser uno de los valores permitidos.",
            ["{Property} must be greater than {Minimum}."] = "{Property} debe ser mayor que {Minimum}.",
            ["{Property} must be at least {Minimum}."] = "{Property} debe ser como mínimo {Minimum}.",
            ["{Property} must be less than {Maximum}."] = "{Property} debe ser menor que {Maximum}.",
            ["{Property} must be at most {Maximum}."] = "{Property} debe ser como máximo {Maximum}.",
            ["{Property} must be between {Minimum} and {Maximum}."] = "{Property} debe estar entre {Minimum} y {Maximum}.",
            ["{Property} is invalid."] = "{Property} no es válido.",
            ["The instance is required."] = "La instancia es obligatoria.",
            ["The request body is required."] = "El cuerpo de la solicitud es obligatorio.",
            ["{Property} must be greater than {OtherProperty}."] = "{Property} debe ser mayor que {OtherProperty}.",
            ["{Property} must be at least {OtherProperty}."] = "{Property} debe ser como mínimo {OtherProperty}.",
            ["{Property} must be less than {OtherProperty}."] = "{Property} debe ser menor que {OtherProperty}.",
            ["{Property} must be at most {OtherProperty}."] = "{Property} debe ser como máximo {OtherProperty}.",
            ["{Property} must be equal to {OtherProperty}."] = "{Property} debe ser igual a {OtherProperty}.",
            ["{Property} must differ from {OtherProperty}."] = "{Property} debe ser distinto de {OtherProperty}.",
        },
        ["fr"] = new(StringComparer.Ordinal)
        {
            ["{Property} is required."] = "{Property} est requis.",
            ["{Property} must not be empty."] = "{Property} ne doit pas être vide.",
            ["{Property} must not be blank."] = "{Property} ne doit pas être vide ou composé d'espaces.",
            ["{Property} must be a valid email address."] = "{Property} doit être une adresse e-mail valide.",
            ["{Property} must contain at least {MinimumLength} characters."] = "{Property} doit contenir au moins {MinimumLength} caractères.",
            ["{Property} must contain no more than {MaximumLength} characters."] = "{Property} ne doit pas dépasser {MaximumLength} caractères.",
            ["{Property} must contain between {MinimumLength} and {MaximumLength} characters."] = "{Property} doit contenir entre {MinimumLength} et {MaximumLength} caractères.",
            ["{Property} has an invalid format."] = "{Property} a un format non valide.",
            ["{Property} has an unexpected value."] = "{Property} a une valeur inattendue.",
            ["{Property} has a forbidden value."] = "{Property} a une valeur non autorisée.",
            ["{Property} must be one of the allowed values."] = "{Property} doit être l'une des valeurs autorisées.",
            ["{Property} must be greater than {Minimum}."] = "{Property} doit être supérieur à {Minimum}.",
            ["{Property} must be at least {Minimum}."] = "{Property} doit être au moins {Minimum}.",
            ["{Property} must be less than {Maximum}."] = "{Property} doit être inférieur à {Maximum}.",
            ["{Property} must be at most {Maximum}."] = "{Property} doit être au plus {Maximum}.",
            ["{Property} must be between {Minimum} and {Maximum}."] = "{Property} doit être compris entre {Minimum} et {Maximum}.",
            ["{Property} is invalid."] = "{Property} n'est pas valide.",
            ["The instance is required."] = "L'instance est requise.",
            ["The request body is required."] = "Le corps de la requête est requis.",
            ["{Property} must be greater than {OtherProperty}."] = "{Property} doit être supérieur à {OtherProperty}.",
            ["{Property} must be at least {OtherProperty}."] = "{Property} doit être au moins {OtherProperty}.",
            ["{Property} must be less than {OtherProperty}."] = "{Property} doit être inférieur à {OtherProperty}.",
            ["{Property} must be at most {OtherProperty}."] = "{Property} doit être au plus {OtherProperty}.",
            ["{Property} must be equal to {OtherProperty}."] = "{Property} doit être égal à {OtherProperty}.",
            ["{Property} must differ from {OtherProperty}."] = "{Property} doit être différent de {OtherProperty}.",
        },
        ["de"] = new(StringComparer.Ordinal)
        {
            ["{Property} is required."] = "{Property} ist erforderlich.",
            ["{Property} must not be empty."] = "{Property} darf nicht leer sein.",
            ["{Property} must not be blank."] = "{Property} darf nicht nur aus Leerzeichen bestehen.",
            ["{Property} must be a valid email address."] = "{Property} muss eine gültige E-Mail-Adresse sein.",
            ["{Property} must contain at least {MinimumLength} characters."] = "{Property} muss mindestens {MinimumLength} Zeichen enthalten.",
            ["{Property} must contain no more than {MaximumLength} characters."] = "{Property} darf höchstens {MaximumLength} Zeichen enthalten.",
            ["{Property} must contain between {MinimumLength} and {MaximumLength} characters."] = "{Property} muss zwischen {MinimumLength} und {MaximumLength} Zeichen enthalten.",
            ["{Property} has an invalid format."] = "{Property} hat ein ungültiges Format.",
            ["{Property} has an unexpected value."] = "{Property} hat einen unerwarteten Wert.",
            ["{Property} has a forbidden value."] = "{Property} hat einen unzulässigen Wert.",
            ["{Property} must be one of the allowed values."] = "{Property} muss einer der zulässigen Werte sein.",
            ["{Property} must be greater than {Minimum}."] = "{Property} muss größer als {Minimum} sein.",
            ["{Property} must be at least {Minimum}."] = "{Property} muss mindestens {Minimum} sein.",
            ["{Property} must be less than {Maximum}."] = "{Property} muss kleiner als {Maximum} sein.",
            ["{Property} must be at most {Maximum}."] = "{Property} darf höchstens {Maximum} sein.",
            ["{Property} must be between {Minimum} and {Maximum}."] = "{Property} muss zwischen {Minimum} und {Maximum} liegen.",
            ["{Property} is invalid."] = "{Property} ist ungültig.",
            ["The instance is required."] = "Die Instanz ist erforderlich.",
            ["The request body is required."] = "Der Anforderungstext ist erforderlich.",
            ["{Property} must be greater than {OtherProperty}."] = "{Property} muss größer als {OtherProperty} sein.",
            ["{Property} must be at least {OtherProperty}."] = "{Property} muss mindestens {OtherProperty} sein.",
            ["{Property} must be less than {OtherProperty}."] = "{Property} muss kleiner als {OtherProperty} sein.",
            ["{Property} must be at most {OtherProperty}."] = "{Property} darf höchstens {OtherProperty} sein.",
            ["{Property} must be equal to {OtherProperty}."] = "{Property} muss gleich {OtherProperty} sein.",
            ["{Property} must differ from {OtherProperty}."] = "{Property} muss sich von {OtherProperty} unterscheiden.",
        },
        ["it"] = new(StringComparer.Ordinal)
        {
            ["{Property} is required."] = "{Property} è obbligatorio.",
            ["{Property} must not be empty."] = "{Property} non può essere vuoto.",
            ["{Property} must not be blank."] = "{Property} non può essere lasciato in bianco.",
            ["{Property} must be a valid email address."] = "{Property} deve essere un indirizzo email valido.",
            ["{Property} must contain at least {MinimumLength} characters."] = "{Property} deve contenere almeno {MinimumLength} caratteri.",
            ["{Property} must contain no more than {MaximumLength} characters."] = "{Property} non può contenere più di {MaximumLength} caratteri.",
            ["{Property} must contain between {MinimumLength} and {MaximumLength} characters."] = "{Property} deve contenere tra {MinimumLength} e {MaximumLength} caratteri.",
            ["{Property} has an invalid format."] = "{Property} ha un formato non valido.",
            ["{Property} has an unexpected value."] = "{Property} ha un valore imprevisto.",
            ["{Property} has a forbidden value."] = "{Property} ha un valore non consentito.",
            ["{Property} must be one of the allowed values."] = "{Property} deve essere uno dei valori consentiti.",
            ["{Property} must be greater than {Minimum}."] = "{Property} deve essere maggiore di {Minimum}.",
            ["{Property} must be at least {Minimum}."] = "{Property} deve essere almeno {Minimum}.",
            ["{Property} must be less than {Maximum}."] = "{Property} deve essere minore di {Maximum}.",
            ["{Property} must be at most {Maximum}."] = "{Property} deve essere al massimo {Maximum}.",
            ["{Property} must be between {Minimum} and {Maximum}."] = "{Property} deve essere compreso tra {Minimum} e {Maximum}.",
            ["{Property} is invalid."] = "{Property} non è valido.",
            ["The instance is required."] = "L'istanza è obbligatoria.",
            ["The request body is required."] = "Il corpo della richiesta è obbligatorio.",
            ["{Property} must be greater than {OtherProperty}."] = "{Property} deve essere maggiore di {OtherProperty}.",
            ["{Property} must be at least {OtherProperty}."] = "{Property} deve essere almeno {OtherProperty}.",
            ["{Property} must be less than {OtherProperty}."] = "{Property} deve essere minore di {OtherProperty}.",
            ["{Property} must be at most {OtherProperty}."] = "{Property} deve essere al massimo {OtherProperty}.",
            ["{Property} must be equal to {OtherProperty}."] = "{Property} deve essere uguale a {OtherProperty}.",
            ["{Property} must differ from {OtherProperty}."] = "{Property} deve essere diverso da {OtherProperty}.",
        },
    };
}
