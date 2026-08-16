namespace eQuantic.Validation;

/// <summary>Resolves a validation message from stable rule metadata.</summary>
public interface IValidationMessageProvider
{
    /// <summary>Returns the final message for a validation failure.</summary>
    string Resolve(ValidationMessageDescriptor descriptor);
}
