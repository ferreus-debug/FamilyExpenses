namespace FamilyExpenses.Application.Abstractions;

/// <summary>Looks up display names of users (Identity lives in Infrastructure).</summary>
public interface IUserDirectory
{
    Task<IReadOnlyDictionary<string, string>> GetDisplayNamesAsync(
        IEnumerable<string> userIds,
        CancellationToken cancellationToken = default);
}
