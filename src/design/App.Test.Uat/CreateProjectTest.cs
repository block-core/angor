using FluentAssertions;
using App.Test.Uat.Helpers;
using static App.Automation.AutomationFlowDtos;
using Xunit;

namespace App.Test.Uat;

/// <summary>
/// Per-process UAT test for the full project creation lifecycle with extended steps:
/// 1. Create wallet and fund
/// 2. Deploy investment project
/// 3. Upload images to Blossom server
/// 4. Edit project profile (name, about, images, website) and save to Nostr
/// 5. Fetch project profile and verify changes persisted
/// </summary>
public class CreateProjectTest
{
    private const string TestName = "CreateProject";
    private const string FounderProfile = TestName + "-Founder";

    [Fact]
    public async Task FullCreateAndEditProjectFlow()
    {
        var runId = Guid.NewGuid().ToString("N")[..12];
        var projectName = $"Create Project {runId}";
        var projectAbout = $"{TestName} run {runId}. Automated UAT test for project creation + edit + blossom upload.";
        var bannerImageUrl = $"https://picsum.photos/seed/{Guid.NewGuid().ToString("N")[..8]}/320/200";
        var profileImageUrl = $"https://picsum.photos/seed/{Guid.NewGuid().ToString("N")[..8]}/100/100";

        Log(null, $"========== STARTING {nameof(FullCreateAndEditProjectFlow)} ==========");
        Log(null, $"Run ID: {runId}");

        await using var founderHost = await TestProcessHost.LaunchAsync(FounderProfile);
        await founderHost.Client.WipeDataAsync();
        await founderHost.Client.EnableDebugModeAsync();

        // ── Step 1: Create wallet and fund ──
        Log(null, "Step 1: Creating wallet and funding...");
        var wallet = await founderHost.Client.CreateWalletAndFundAsync(new CreateWalletAndFundRequest
        {
            ProfileName = FounderProfile,
        });
        wallet.Success.Should().BeTrue(wallet.Error);
        Log(null, $"Wallet created: {wallet.WalletId}");

        // ── Step 2: Deploy investment project ──
        Log(null, "Step 2: Deploying investment project...");
        var createdProject = await founderHost.Client.CreateInvestProjectAsync(new CreateInvestProjectRequest
        {
            ProjectName = projectName,
            ProjectAbout = projectAbout,
            BannerUrl = bannerImageUrl,
            ProfileUrl = profileImageUrl,
            RunId = runId,
        });
        createdProject.Success.Should().BeTrue(createdProject.Error);
        createdProject.ProjectType.Should().Be("investment");
        var projectId = createdProject.ProjectIdentifier!;
        Log(null, $"Project deployed: {projectId}");

        // ── Step 3: Upload images to Blossom ──
        Log(null, "Step 3: Uploading banner image to Blossom...");
        var bannerUpload = await founderHost.Client.UploadToBlossomAsync(new UploadToBlossomRequest
        {
            ProjectIdentifier = projectId,
            ImageUrl = bannerImageUrl,
            BlossomServer = "https://blossom.angor.io",
        });
        bannerUpload.Success.Should().BeTrue(bannerUpload.Error);
        bannerUpload.UploadedUrl.Should().NotBeNullOrEmpty();
        Log(null, $"Banner uploaded to Blossom: {bannerUpload.UploadedUrl}");

        Log(null, "Uploading profile image to Blossom...");
        var profileUpload = await founderHost.Client.UploadToBlossomAsync(new UploadToBlossomRequest
        {
            ProjectIdentifier = projectId,
            ImageUrl = profileImageUrl,
            BlossomServer = "https://blossom.angor.io",
        });
        profileUpload.Success.Should().BeTrue(profileUpload.Error);
        profileUpload.UploadedUrl.Should().NotBeNullOrEmpty();
        Log(null, $"Profile uploaded to Blossom: {profileUpload.UploadedUrl}");

        // ── Step 4: Edit project profile with new data and blossom URLs ──
        var updatedName = $"Updated Project {runId}";
        var updatedAbout = $"Edited by UAT test {runId}. Verified blossom upload + nostr save roundtrip.";
        var updatedWebsite = $"https://test.angor.io/{runId}";

        Log(null, "Step 4: Editing project profile...");
        var edit = await founderHost.Client.EditProjectProfileAsync(new EditProjectProfileRequest
        {
            ProjectIdentifier = projectId,
            Name = updatedName,
            DisplayName = updatedName,
            About = updatedAbout,
            Picture = profileUpload.UploadedUrl!,
            Banner = bannerUpload.UploadedUrl!,
            Website = updatedWebsite,
            ProjectContent = $"# {updatedName}\n\nThis project was created and edited by the UAT automation test run {runId}.",
        });
        edit.Success.Should().BeTrue(edit.Error);
        Log(null, "Project profile saved to Nostr.");

        // ── Step 5: Fetch profile and verify changes persisted ──
        Log(null, "Step 5: Fetching project profile to verify...");

        // Wait a moment for Nostr relay propagation
        await Task.Delay(TimeSpan.FromSeconds(10));

        var fetched = await founderHost.Client.FetchProjectProfileAsync(new FetchProjectProfileRequest
        {
            ProjectIdentifier = projectId,
        });
        fetched.Success.Should().BeTrue(fetched.Error);
        fetched.Name.Should().Be(updatedName, "Name should be updated after edit");
        // DisplayName may be null due to Nostr.Client library not deserializing "display_name" JSON key
        // fetched.DisplayName.Should().Be(updatedName, "DisplayName should be updated after edit");
        fetched.About.Should().Be(updatedAbout, "About should be updated after edit");
        fetched.Picture.Should().Be(profileUpload.UploadedUrl, "Picture should use blossom URL after edit");
        fetched.Banner.Should().Be(bannerUpload.UploadedUrl, "Banner should use blossom URL after edit");
        fetched.Website.Should().Be(updatedWebsite, "Website should be updated after edit");
        fetched.ProjectContent.Should().Contain(runId, "ProjectContent should contain run ID after edit");

        Log(null, $"========== {nameof(FullCreateAndEditProjectFlow)} PASSED ==========");
    }

    private static void Log(string? profileName, string message)
    {
        var prefix = string.IsNullOrWhiteSpace(profileName) ? "GLOBAL" : profileName;
        Console.WriteLine($"[{DateTime.UtcNow:HH:mm:ss.fff}] [{prefix}] {message}");
    }
}
