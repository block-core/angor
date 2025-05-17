
namespace Angor.Client.Services;

public interface IFeatureFlagService
{
    public bool IsFeatureEnabled(string featureName);
    public void SetFeatureFlag(string featureName, bool isEnabled);
    public Dictionary<string, bool> GetAllFeatureFlags();
    public void SetAllFeatureFlags(Dictionary<string, bool> featureFlags);
    public Dictionary<string, bool> GetDefaultFeatureFlags(string network);
    bool IsFeatureHWSupportEnabled();
}
