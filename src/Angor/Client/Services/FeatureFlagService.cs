using Angor.Client.Storage;

namespace Angor.Client.Services;

public class FeatureFlagService : IFeatureFlagService
{
    private readonly IClientStorage _storage;
    private readonly Dictionary<string, bool> _featureFlags;
    public FeatureFlagService(IClientStorage storage)
    {
        _storage = storage;
        _featureFlags = _storage.getFeatureFlags();
    }

    public bool IsFeatureEnabled(string featureName)
    {
        return _featureFlags.TryGetValue(featureName, out var isEnabled) && isEnabled;
    }

    public void SetFeatureFlag(string featureName, bool isEnabled)
    {
        if (_featureFlags.ContainsKey(featureName))
        {
            _featureFlags[featureName] = isEnabled;
            _storage.setFeatureFlags(_featureFlags);
        }
    }

    public Dictionary<string, bool> GetAllFeatureFlags()
    {
        return _storage.getFeatureFlags();
    }

    public bool IsFeatureHWSupportEnabled() => IsFeatureEnabled("HW_Support");
}