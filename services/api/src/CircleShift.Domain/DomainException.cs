namespace CircleShift.Domain;

/// <summary>
/// A business rule was broken. <see cref="Code"/> is stable and safe to return
/// to clients; the message is for logs and developers.
/// </summary>
public sealed class DomainException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}
