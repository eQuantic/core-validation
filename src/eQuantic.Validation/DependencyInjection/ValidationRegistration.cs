namespace eQuantic.Validation;

internal sealed class ValidationRegistration
{
    public ValidationRegistration(Type modelType, Func<IServiceProvider, IValidator> resolve)
    {
        ModelType = modelType;
        Resolve = resolve;
    }

    public Type ModelType { get; }

    public Func<IServiceProvider, IValidator> Resolve { get; }
}
