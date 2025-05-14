using System;
using Angor.Client.Storage;

namespace Angor.Client.Services;

public interface IFeatureFlagService
{
    public bool IsFeatureEnabled(string featureName);
    public void SetFeatureFlag(string featureName, bool isEnabled);
    public Dictionary<string, bool> GetAllFeatureFlags();
}
