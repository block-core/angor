using Angor.Shared.Models;
using ReactiveUI;
using System;
using System.Reactive;

namespace AngorApp.Sections.Settings;

internal class SettingsUrlViewModel : ReactiveObject
{
    string url;
    bool isPrimary;

    public SettingsUrlViewModel(string url, bool isPrimary, Action<SettingsUrlViewModel> remove, Action<SettingsUrlViewModel>? setPrimary = null)
    {
        this.url = url;
        this.isPrimary = isPrimary;
        Remove = ReactiveCommand.Create(() => remove(this));
        SetPrimary = setPrimary != null ? ReactiveCommand.Create(() => setPrimary(this)) : null;
    }

    public string Url
    {
        get => url;
        set => this.RaiseAndSetIfChanged(ref url, value);
    }

    public bool IsPrimary
    {
        get => isPrimary;
        set => this.RaiseAndSetIfChanged(ref isPrimary, value);
    }

    public ReactiveCommand<Unit, Unit> Remove { get; }
    public ReactiveCommand<Unit, Unit>? SetPrimary { get; }

    public SettingsUrl ToModel() => new() { Url = Url, IsPrimary = IsPrimary };
}
