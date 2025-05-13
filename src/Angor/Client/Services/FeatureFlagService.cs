using Blazored.LocalStorage;

namespace Angor.Client.Services;

public class FeatureFlagService : IFeatureFlagService
{
    private readonly ISyncLocalStorageService _storage; // Injected storage service
    private readonly Dictionary<string, bool> _featureFlags;
    public FeatureFlagService(ISyncLocalStorageService storage)
    {
        Console.WriteLine("Feature flags fetched");
        _storage = storage;
        _featureFlags = _storage.GetItem<Dictionary<string, bool>>("FeatureFlags") ?? new()
        {
            {"HW_Support", false}
        };
    }

    public bool IsFeatureEnabled(string featureName)
    {
        return _featureFlags.TryGetValue(featureName, out var isEnabled) && isEnabled;
    }

    public void SetFeatureFlag(string featureName, bool isEnabled)
    {
        Console.WriteLine(featureName + "is" + isEnabled);
        if (_featureFlags.ContainsKey(featureName))
        {
            _featureFlags[featureName] = isEnabled;
            _storage.SetItem("FeatureFlags", _featureFlags);
        }
    }

    public Dictionary<string, bool> GetAllFeatureFlags()
    {
        Console.WriteLine("Get all feature flags");
        return new Dictionary<string, bool>(_featureFlags);
    }
}