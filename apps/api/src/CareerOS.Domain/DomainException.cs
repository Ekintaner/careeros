namespace CareerOS.Domain;

/// <summary>A violation of an explicit domain rule; HTTP mapping belongs to the API.</summary>
public abstract class DomainException(string message) : Exception(message);
