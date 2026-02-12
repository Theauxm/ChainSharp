using ChainSharp.Effect.Services.EffectRegistry;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Radzen;

namespace ChainSharp.Effect.Dashboard.Components.Pages.Settings;

public partial class EffectSettingsPage
{
    [Inject]
    private IServiceProvider ServiceProvider { get; set; } = default!;

    [Inject]
    private NotificationService NotificationService { get; set; } = default!;

    private IEffectRegistry? _registry;
    private bool _available;
    private List<EffectEntry> _effects = [];

    protected override void OnInitialized()
    {
        _registry = ServiceProvider.GetService<IEffectRegistry>();
        _available = _registry is not null;

        if (_available)
            LoadEffects();
    }

    private void LoadEffects()
    {
        _effects = _registry!
            .GetAll()
            .Select(
                kvp =>
                    new EffectEntry
                    {
                        FactoryType = kvp.Key,
                        Name = kvp.Key.Name,
                        FullName = kvp.Key.FullName ?? kvp.Key.Name,
                        Enabled = kvp.Value,
                    }
            )
            .OrderBy(e => e.Name)
            .ToList();
    }

    private void OnToggle(EffectEntry entry)
    {
        if (_registry is null)
            return;

        if (entry.Enabled)
            _registry.Enable(entry.FactoryType);
        else
            _registry.Disable(entry.FactoryType);

        NotificationService.Notify(
            new NotificationMessage
            {
                Severity = entry.Enabled
                    ? NotificationSeverity.Success
                    : NotificationSeverity.Warning,
                Summary = entry.Enabled ? "Effect Enabled" : "Effect Disabled",
                Detail =
                    $"{entry.Name} has been {(entry.Enabled ? "enabled" : "disabled")}. Change takes effect on the next workflow execution.",
                Duration = 3000,
            }
        );
    }

    private void EnableAll()
    {
        foreach (var entry in _effects)
        {
            entry.Enabled = true;
            _registry!.Enable(entry.FactoryType);
        }

        NotificationService.Notify(
            new NotificationMessage
            {
                Severity = NotificationSeverity.Success,
                Summary = "All Effects Enabled",
                Duration = 3000,
            }
        );
    }

    private void DisableAll()
    {
        foreach (var entry in _effects)
        {
            entry.Enabled = false;
            _registry!.Disable(entry.FactoryType);
        }

        NotificationService.Notify(
            new NotificationMessage
            {
                Severity = NotificationSeverity.Warning,
                Summary = "All Effects Disabled",
                Duration = 3000,
            }
        );
    }

    private class EffectEntry
    {
        public required Type FactoryType { get; init; }
        public required string Name { get; init; }
        public required string FullName { get; init; }
        public bool Enabled { get; set; }
    }
}
