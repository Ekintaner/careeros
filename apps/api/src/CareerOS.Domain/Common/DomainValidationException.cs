namespace CareerOS.Domain.Common;

public sealed class DomainValidationException(string message) : DomainException(message);
