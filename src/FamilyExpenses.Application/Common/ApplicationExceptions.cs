namespace FamilyExpenses.Application.Common;

/// <summary>The resource does not exist, or the user may not know it exists.</summary>
public sealed class NotFoundException : Exception
{
    public NotFoundException()
        : this("Det du leder efter findes ikke.")
    {
    }

    public NotFoundException(string message)
        : base(message)
    {
    }

    public NotFoundException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

/// <summary>The user is known but not allowed to perform the action.</summary>
public sealed class ForbiddenException : Exception
{
    public ForbiddenException()
        : this("Du har ikke adgang til at gøre dette.")
    {
    }

    public ForbiddenException(string message)
        : base(message)
    {
    }

    public ForbiddenException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

/// <summary>Someone else changed the same data at the same time; the change was not saved and can be retried.</summary>
public sealed class ConcurrencyConflictException : Exception
{
    public const string DefaultMessage = "Nogen har lige ændret turen – prøv igen.";

    public ConcurrencyConflictException()
        : this(DefaultMessage)
    {
    }

    public ConcurrencyConflictException(string message)
        : base(message)
    {
    }

    public ConcurrencyConflictException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
