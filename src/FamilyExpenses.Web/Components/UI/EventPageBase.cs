using FamilyExpenses.Application.Events;
using Microsoft.AspNetCore.Components;

namespace FamilyExpenses.Web.Components.UI;

/// <summary>Base for pages under /events/{EventId}: loads the event details (and with them access).</summary>
public abstract class EventPageBase : AppComponentBase
{
    [Parameter]
    public Guid EventId { get; set; }

    [Inject]
    protected EventService Events { get; set; } = default!;

    protected EventDetails? Details { get; private set; }

    protected override async Task OnParametersSetAsync() => await ReloadAsync();

    protected virtual async Task ReloadAsync() => Details = await LoadAsync(() => Events.GetAsync(EventId));
}
