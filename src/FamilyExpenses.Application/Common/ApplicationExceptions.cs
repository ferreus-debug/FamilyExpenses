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
