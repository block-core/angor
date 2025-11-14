using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Angor.Shared;
using Angor.Shared.Models;
using CSharpFunctionalExtensions;

namespace Angor.Contests.CrossCutting;

public class NetworkStorage : INetworkStorage
{
    private const string NetworkSettingsFileName = "network-settings.json";

    private readonly IStore store;
    private string currentNetwork;

    public NetworkStorage(IStore store, string initialNetwork = "Angornet")
    {
        this.store = store ?? throw new ArgumentNullException(nameof(store));
        currentNetwork = Normalize(initialNetwork);
    }

    public SettingsInfo GetSettings() => LoadSettingsForNetwork(currentNetwork);

    public void SetSettings(SettingsInfo settingsInfo)
    {
        ArgumentNullException.ThrowIfNull(settingsInfo);
        SaveSettingsForNetwork(currentNetwork, settingsInfo);
    }

    public void SetNetwork(string network)
    {
        currentNetwork = Normalize(network);
    }

    public string GetNetwork() => currentNetwork;

    private SettingsInfo LoadSettingsForNetwork(string network)
    {
        var scopedKey = NetworkScopedKey(network);
        var result = store.Load<SettingsInfo>(scopedKey).GetAwaiter().GetResult();

        return result.IsSuccess
            ? Clone(result.Value)
            : new SettingsInfo();
    }

    private void SaveSettingsForNetwork(string network, SettingsInfo settingsInfo)
    {
        var scopedKey = NetworkScopedKey(network);
        store.Save(scopedKey, Clone(settingsInfo)).GetAwaiter().GetResult();
    }

    private static string NetworkScopedKey(string network) =>
        Path.Combine(network, NetworkSettingsFileName);

    private static SettingsInfo Clone(SettingsInfo source) => new()
    {
        Explorers = CloneList(source.Explorers),
        Indexers = CloneList(source.Indexers),
        Relays = CloneList(source.Relays),
        ChatApps = CloneList(source.ChatApps),
    };

    private static List<SettingsUrl> CloneList(List<SettingsUrl>? source) =>
        source?.Select(Clone).ToList() ?? new List<SettingsUrl>();

    private static SettingsUrl Clone(SettingsUrl url) => new()
    {
        Name = url.Name,
        Url = url.Url,
        IsPrimary = url.IsPrimary,
        Status = url.Status,
        LastCheck = url.LastCheck
    };

    private static string Normalize(string? network) =>
        string.IsNullOrWhiteSpace(network) ? "Angornet" : network;
}
