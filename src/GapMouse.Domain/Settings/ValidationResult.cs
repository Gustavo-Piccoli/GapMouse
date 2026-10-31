namespace GapMouse.Domain.Settings;

public sealed class ValidationResult<T>
{
    private readonly T? _value;

    internal ValidationResult(T value)
    {
        _value = value;
        Errors = [];
    }

    internal ValidationResult(IReadOnlyList<SettingsError> errors) => Errors = errors;

    public bool IsValid => Errors.Count == 0;

    /// <summary>The validated value; throws when the result is invalid.</summary>
    public T Value => IsValid
        ? _value!
        : throw new InvalidOperationException("Cannot read the value of an invalid result.");

    public IReadOnlyList<SettingsError> Errors { get; }
}
