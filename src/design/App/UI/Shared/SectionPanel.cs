using System.Diagnostics;
using Avalonia.Controls;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace App.UI.Shared;

/// <summary>
/// A Panel that holds all section views as children and switches between them
/// by toggling IsVisible. This avoids the Avalonia logical-tree detach/reattach
/// cost (~250ms on Android) that ContentControl content-swap incurs.
///
/// Each child must be a Control. Children that implement <see cref="ISectionView"/>
/// receive OnBecameActive/OnBecameInactive lifecycle calls on switch.
///
/// Usage (from ShellView code-behind, mobile only):
///   var panel = new SectionPanel();
///   panel.AddSection("Home", homeView);
///   panel.AddSection("Find Projects", findProjectsView);
///   ...
///   panel.ActivateSection("Find Projects");
/// </summary>
public class SectionPanel : Panel
{
    private readonly Dictionary<string, Control> _sections = new();
    private string? _activeKey;
    private static ILogger? _logger;
    private static ILogger Logger =>
        _logger ??= App.Services.GetRequiredService<ILoggerFactory>().CreateLogger("SectionPanel");

    /// <summary>
    /// Register a section view under the given key. The view is added as a child
    /// with IsVisible=false. If the view is already a child, it is not re-added.
    /// </summary>
    public void AddSection(string key, Control view)
    {
        if (_sections.ContainsKey(key)) return;

        view.IsVisible = false;
        _sections[key] = view;

        if (!Children.Contains(view))
            Children.Add(view);
    }

    /// <summary>
    /// Switch to the section identified by <paramref name="key"/>.
    /// Hides the previously active section (calling OnBecameInactive) and shows
    /// the new one (calling OnBecameActive). Returns the activated Control, or
    /// null if the key is not registered.
    /// </summary>
    public Control? ActivateSection(string key)
    {
        if (!_sections.TryGetValue(key, out var target))
            return null;

        if (_activeKey == key)
        {
            // Already active — still call OnBecameActive for data refresh semantics
            (target as ISectionView)?.OnBecameActive();
            return target;
        }

        var sw = Stopwatch.StartNew();

        // Deactivate previous
        if (_activeKey != null && _sections.TryGetValue(_activeKey, out var previous))
        {
            previous.IsVisible = false;
            (previous as ISectionView)?.OnBecameInactive();
        }

        // Activate new
        _activeKey = key;
        target.IsVisible = true;
        (target as ISectionView)?.OnBecameActive();

        sw.Stop();
        Debug.WriteLine($"[SectionPanel] ActivateSection key={key} ms={sw.ElapsedMilliseconds}");
        Logger.LogInformation("[SectionPanel] ActivateSection key={Key} ms={Ms}", key, sw.ElapsedMilliseconds);

        return target;
    }

    /// <summary>The currently active section key, or null if none.</summary>
    public string? ActiveKey => _activeKey;

    /// <summary>Get a registered section view by key.</summary>
    public Control? GetSection(string key) => _sections.GetValueOrDefault(key);
}
