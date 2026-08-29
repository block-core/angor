namespace App.Test.Uat.Helpers;

/// <summary>
/// Common interface for UAT test hosts (desktop process or Android device).
/// Both provide a <see cref="TestAutomationClient"/> and support async disposal.
/// </summary>
public interface ITestHost : IAsyncDisposable
{
    TestAutomationClient Client { get; }
    string ProfileName { get; }
    int Port { get; }
}
