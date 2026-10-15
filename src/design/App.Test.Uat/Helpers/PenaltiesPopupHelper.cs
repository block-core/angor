using FluentAssertions;

namespace App.Test.Uat.Helpers;

public static class PenaltiesPopupHelper
{
    /// <summary>
    /// Opens the Penalties popup from the Funded tab and asserts it lists exactly the investment
    /// that was just recovered to penalty. Retries LoadPenaltiesAsync to absorb indexer lag.
    /// Assumes the project was created with PenaltyDays = 0.
    /// </summary>
    public static async Task VerifyPenaltiesPopupAsync(
        TestProcessHost host,
        string profileName,
        string projectId,
        string projectName)
    {
        Log(profileName, "Opening Penalties popup to verify In Penalty entry...");
        await host.Client.NavigateAsync("Funded");
        await host.Client.WaitForControlAsync("PenaltiesButton", TimeSpan.FromSeconds(30));
        await host.Client.ClickAsync("PenaltiesButton");

        // PenaltiesModal is mounted in the shell modal overlay; its CloseButton confirms it opened
        await host.Client.WaitForControlAsync("CloseButton", TimeSpan.FromSeconds(15));

        // Clicking the button kicks off LoadPenaltiesAsync; wait for the initial load to finish,
        // then retry the wallet-wide penalty scan until the entry appears (indexer may lag).
        await host.Client.WaitForVmPropertyAsync(
            "PortfolioViewModel", "IsLoadingPenalties", "False", TimeSpan.FromMinutes(2));

        var deadline = DateTime.UtcNow + TimeSpan.FromMinutes(3);
        while (DateTime.UtcNow < deadline)
        {
            var hasPenalties = await host.Client.GetVmPropertyAsync("PortfolioViewModel", "HasPenalties");
            if (string.Equals(hasPenalties, "True", StringComparison.OrdinalIgnoreCase))
            {
                break;
            }

            await Task.Delay(TimeSpan.FromSeconds(5));
            await host.Client.InvokeVmAsync("PortfolioViewModel", "LoadPenaltiesAsync");
            await host.Client.WaitForVmPropertyAsync(
                "PortfolioViewModel", "IsLoadingPenalties", "False", TimeSpan.FromMinutes(1));
        }

        var penaltyCount = await host.Client.GetVmPropertyAsync("PortfolioViewModel", "Penalties.Count");
        penaltyCount.Should().Be("1", "the investor recovered exactly one investment to penalty");

        var penaltyProjectId = await host.Client.GetVmPropertyAsync("PortfolioViewModel", "Penalties[0].ProjectIdentifier");
        penaltyProjectId.Should().Be(projectId);

        var penaltyProjectName = await host.Client.GetVmPropertyAsync("PortfolioViewModel", "Penalties[0].ProjectName");
        penaltyProjectName.Should().Be(projectName);

        var penaltyDaysRemaining = await host.Client.GetVmPropertyAsync("PortfolioViewModel", "Penalties[0].DaysRemaining");
        penaltyDaysRemaining.Should().Be("0", "the project was created with PenaltyDays = 0");

        var penaltyStatus = await host.Client.GetVmPropertyAsync("PortfolioViewModel", "Penalties[0].StatusText");
        penaltyStatus.Should().Be("Penalty release available now");

        Log(profileName, $"Penalties popup verified: {penaltyProjectName} ({penaltyProjectId}), status '{penaltyStatus}'");

        // Close the popup so the subsequent penaltyRelease flow starts from a clean Funded tab
        await host.Client.ClickAsync("CloseButton");
    }

    private static void Log(string profileName, string message)
    {
        Console.WriteLine($"[{DateTime.UtcNow:HH:mm:ss.fff}] [{profileName}] {message}");
    }
}
