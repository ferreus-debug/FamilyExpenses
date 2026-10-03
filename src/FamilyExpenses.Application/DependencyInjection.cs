using FamilyExpenses.Application.Events;
using FamilyExpenses.Application.Expenses;
using FamilyExpenses.Application.Invitations;
using FamilyExpenses.Application.Settlements;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace FamilyExpenses.Application;

public static class DependencyInjection
{
    /// <summary>Registers the use-case services. They need an <c>ICurrentUser</c> from the host.</summary>
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.AddScoped<EventService>();
        services.AddScoped<HouseholdService>();
        services.AddScoped<ExpenseService>();
        services.AddScoped<SettlementService>();
        services.AddScoped<InvitationService>();
        return services;
    }
}
