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

    // Saved-state snapshot for dirty tracking
    private Dictionary<Type, bool> _savedStates = new();

    private bool IsDirty =>
        _effects.Any(
            e => e.Toggleable && e.Enabled != _savedStates.GetValueOrDefault(e.FactoryType)
        );

    protected override void OnInitialized()
    {
        _registry = ServiceProvider.GetService<IEffectRegistry>();
        _available = _registry is not null;

        if (_available)
        {
            LoadEffects();
            SnapshotSavedState();
        }
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
                        Toggleable = _registry.IsToggleable(kvp.Key),
                    }
            )
            .OrderBy(e => e.Name)
            .ToList();
    }

    private void EnableAll()
    {
        foreach (var entry in _effects.Where(e => e.Toggleable))
            entry.Enabled = true;
    }

    private void DisableAll()
    {
        foreach (var entry in _effects.Where(e => e.Toggleable))
            entry.Enabled = false;
    }

    private void Save()
    {
        if (_registry is null)
            return;

        foreach (var entry in _effects.Where(e => e.Toggleable))
        {
            if (entry.Enabled)
                _registry.Enable(entry.FactoryType);
            else
                _registry.Disable(entry.FactoryType);
        }

        SnapshotSavedState();

        NotificationService.Notify(
            new NotificationMessage
            {
                Severity = NotificationSeverity.Success,
                Summary = "Settings Saved",
                Detail =
                    "Effect settings updated. Changes take effect on the next workflow execution.",
                Duration = 4000,
            }
        );
    }

    private void ResetDefaults()
    {
        foreach (var entry in _effects.Where(e => e.Toggleable))
            entry.Enabled = _savedStates.GetValueOrDefault(entry.FactoryType);
    }

    private void SnapshotSavedState()
    {
        _savedStates = _effects.ToDictionary(e => e.FactoryType, e => e.Enabled);
    }

    private class EffectEntry
    {
        public required Type FactoryType { get; init; }
        public required string Name { get; init; }
        public required string FullName { get; init; }
        public bool Enabled { get; set; }
        public required bool Toggleable { get; init; }
    }
}
