namespace CareerOS.Domain.Common;

public interface ICreatedEntity
{
    DateTime CreatedAtUtc { get; }
}

public interface IAuditedEntity : ICreatedEntity
{
    DateTime UpdatedAtUtc { get; }
}
