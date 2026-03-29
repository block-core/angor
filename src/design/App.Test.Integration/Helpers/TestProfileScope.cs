using App.Composition;

namespace App.Test.Integration.Helpers;

public sealed class TestProfileScope : IDisposable
{
    private readonly string previousProfileName;

    private TestProfileScope(string previousProfileName)
    {
        this.previousProfileName = previousProfileName;
    }

    public static TestProfileScope For(string profileName)
    {
        var previousProfileName = TestProfileNameProvider.Current;
        TestProfileNameProvider.Current = ProfileNameResolver.GetProfileName([$"{ProfileNameResolver.ProfileOption}={profileName}"]);
        TestAppBuilder.RefreshServicesForCurrentProfile();
        return new TestProfileScope(previousProfileName);
    }

    public void Dispose()
    {
        TestProfileNameProvider.Current = previousProfileName;
        TestAppBuilder.RefreshServicesForCurrentProfile();
    }
}
