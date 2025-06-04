using Angor.Client.Storage;
using Angor.Shared;

namespace Angor.Client.Services;

public class FeatureFlagService : IFeatureFlagService
{
    private readonly IClientStorage _storage;
    private readonly INetworkConfiguration _networkConfiguration;
    private Dictionary<string, bool> _featureFlags;
    public FeatureFlagService(IClientStorage storage, INetworkConfiguration networkConfiguration)
    {
        _storage = storage;
        _networkConfiguration = networkConfiguration;
    }

    public bool IsFeatureEnabled(string featureName)
    {
        if (_featureFlags == null)
            GetAllFeatureFlags();

        return _featureFlags.TryGetValue(featureName, out var isEnabled) && isEnabled;
    }

    public Dictionary<string, bool> GetAllFeatureFlags()
    {
        _featureFlags = _storage.GetFeatureFlags() ?? [];
        return _featureFlags;
    }

    public Dictionary<string, bool> GetDefaultFeatureFlags(string network)
    {
        return _networkConfiguration.GetDefaultFeatureFlags(network);
    }

    public void SetAllFeatureFlags(Dictionary<string, bool> featureFlags)
    {
        _featureFlags = featureFlags;
        _storage.SetFeatureFlags(featureFlags);
    }

    public bool IsFeatureHWSupportEnabled() => IsFeatureEnabled("HW_Support");
}