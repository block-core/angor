using Angor.Shared.Models;
using ReactiveUI;
using System;
using System.Reactive;

namespace AngorApp.UI.Sections.Settings;

public class SettingsUrlViewModel : ReactiveObject
{
    string url;
    bool isPrimary;
    string? name;
    UrlStatus status;
    DateTime lastCheck;

    public SettingsUrlViewModel(string url, bool isPrimary, UrlStatus status, DateTime lastCheck, Action<SettingsUrlViewModel> remove, Action<SettingsUrlViewModel>? setPrimary = null, string? name = null)
    {
        this.url = url;
        this.isPrimary = isPrimary;
        this.status = status;
        this.lastCheck = lastCheck;
        this.name = name;
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

    public string? Name
    {
        get => name;
        set => this.RaiseAndSetIfChanged(ref name, value);
    }

    public UrlStatus Status
    {
        get => status;
        set => this.RaiseAndSetIfChanged(ref status, value);
    }

    public DateTime LastCheck
    {
        get => lastCheck;
        set => this.RaiseAndSetIfChanged(ref lastCheck, value);
    }

    public ReactiveCommand<Unit, Unit> Remove { get; }
    public ReactiveCommand<Unit, Unit>? SetPrimary { get; }

    public SettingsUrl ToModel() => new()
    {
        Url = Url,
        IsPrimary = IsPrimary,
        Status = Status,
        LastCheck = LastCheck,
        Name = Name ?? string.Empty
    };
}
