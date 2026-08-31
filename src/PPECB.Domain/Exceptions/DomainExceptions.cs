namespace PPECB.Domain.Exceptions;

/// <summary>Base type for expected, business-rule failures (as opposed to bugs).</summary>
public abstract class DomainException : Exception
{
    protected DomainException(string message) : base(message) { }
}

/// <summary>A requested entity does not exist, or is not visible to the current user.</summary>
public class NotFoundException : DomainException
{
    public NotFoundException(string name, object key)
        : base($"{name} with identifier '{key}' was not found.") { }
}

/// <summary>A business rule was violated, e.g. a duplicate category code.</summary>
public class BusinessRuleException : DomainException
{
    public BusinessRuleException(string message) : base(message) { }
}

/// <summary>
/// Another user changed the record between the read and the write. The Infrastructure
/// layer raises this when EF Core reports an optimistic-concurrency failure.
/// </summary>
public class ConcurrencyConflictException : DomainException
{
    public ConcurrencyConflictException(string entityName)
        : base($"This {entityName} was changed by someone else after you loaded it. " +
               "Reload the record and apply your changes again.") { }
}

/// <summary>
/// A unique index rejected the write. Raised by the Infrastructure layer so the
/// Application layer can react without knowing about SQL Server error numbers.
/// </summary>
public class DuplicateKeyException : DomainException
{
    public DuplicateKeyException(string message) : base(message) { }
}

/// <summary>Input failed validation. Carries per-field messages for the client.</summary>
public class ValidationException : DomainException
{
    public IDictionary<string, string[]> Errors { get; }

    public ValidationException(IDictionary<string, string[]> errors)
        : base("One or more validation errors occurred.")
    {
        Errors = errors;
    }

    public ValidationException(string field, string message)
        : this(new Dictionary<string, string[]> { [field] = new[] { message } }) { }
}
