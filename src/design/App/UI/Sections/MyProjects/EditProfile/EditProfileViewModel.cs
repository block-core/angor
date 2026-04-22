using System.Collections.ObjectModel;
using Angor.Sdk.Common;
using Angor.Sdk.Funding.Founder.Dtos;
using Angor.Sdk.Funding.Projects;
using Angor.Sdk.Funding.Projects.Domain;
using Angor.Sdk.Funding.Services;
using Angor.Sdk.Funding.Shared;
using Angor.Shared.Models;
using Angor.Shared.Services;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using ReactiveUI;
using ReactiveUI.SourceGenerators;

namespace App.UI.Sections.MyProjects.EditProfile;

/// <summary>
/// A single FAQ item for the edit profile FAQ tab.
/// </summary>
public partial class FaqItemViewModel : ReactiveObject
{
    [Reactive] private string _question = "";
    [Reactive] private string _answer = "";
}

/// <summary>
/// A single media item (image or video URL) for the edit profile Media tab.
/// </summary>
public partial class MediaItemViewModel : ReactiveObject
{
    [Reactive] private string _url = "";
    [Reactive] private string _type = "image";
}

/// <summary>
/// ViewModel for the Edit Project Profile screen.
/// Matches the tab structure of https://profile.angor.io/:
/// Profile | Project | FAQ | Members | Media | Relays
/// </summary>
public partial class EditProfileViewModel : ReactiveObject
{
    private readonly MyProjectItemViewModel _project;
    private readonly IProjectAppService _projectAppService;
    private readonly IProjectService _projectService;
    private readonly IRelayService _relayService;
    private readonly ILogger<EditProfileViewModel> _logger;

    public event Action<string>? ToastRequested;

    // ── Active tab ──
    [Reactive] private string _activeTab = "profile";

    // ── Loading / Saving state ──
    [Reactive] private bool _isLoading;
    [Reactive] private bool _isSaving;

    // ── Profile tab ──
    [Reactive] private string _profileName = "";
    [Reactive] private string _profileDisplayName = "";
    [Reactive] private string _profileAbout = "";
    [Reactive] private string _profilePicture = "";
    [Reactive] private string _profileBanner = "";
    [Reactive] private string _profileNip05 = "";
    [Reactive] private string _profileLud16 = "";
    [Reactive] private string _profileWebsite = "";

    // ── Project tab ──
    [Reactive] private string _projectContent = "";

    // ── Members tab ──
    [Reactive] private string _newMemberPubKey = "";

    // ── Media tab ──
    [Reactive] private string _newMediaUrl = "";
    [Reactive] private string _newMediaType = "image";

    // ── Relays tab ──
    [Reactive] private string _newRelayUrl = "";

    // ── Collections ──
    public ObservableCollection<FaqItemViewModel> FaqItems { get; } = new();
    public ObservableCollection<string> Members { get; } = new();
    public ObservableCollection<MediaItemViewModel> MediaItems { get; } = new();
    public ObservableCollection<string> Relays { get; } = new();

    // ── Derived project info ──
    private ProjectSeedDto? _projectSeedDto;

    public string ProjectName => _project.Name;

    public EditProfileViewModel(
        MyProjectItemViewModel project,
        IProjectAppService projectAppService,
        IProjectService projectService,
        IRelayService relayService,
        ILogger<EditProfileViewModel> logger)
    {
        _project = project;
        _projectAppService = projectAppService;
        _projectService = projectService;
        _relayService = relayService;
        _logger = logger;

        // Add one empty FAQ item to start
        FaqItems.Add(new FaqItemViewModel());

        _ = LoadAsync();
    }

    /// <summary>Load existing profile and content data from Nostr relays.</summary>
    public async Task LoadAsync()
    {
        if (IsLoading) return;
        IsLoading = true;

        try
        {
            await LoadProjectSeedDtoAsync();

            var nostrPubKeyHex = _projectSeedDto?.NostrPubKey;
            if (string.IsNullOrEmpty(nostrPubKeyHex))
            {
                _logger.LogWarning("No Nostr public key found for project {ProjectId}", _project.ProjectIdentifier);
                return;
            }

            // Load kind-0 profile metadata
            var metadata = await _relayService.FetchProfileMetadataAsync(nostrPubKeyHex);
            if (metadata != null)
            {
                ProfileName = metadata.Name ?? "";
                ProfileDisplayName = metadata.DisplayName ?? "";
                ProfileAbout = metadata.About ?? "";
                ProfilePicture = metadata.Picture ?? "";
                ProfileBanner = metadata.Banner ?? "";
                ProfileNip05 = metadata.Nip05 ?? "";
                ProfileLud16 = metadata.Lud16 ?? "";
                ProfileWebsite = metadata.Website ?? "";
            }

            // Load project content
            var projectJson = await _relayService.FetchAppSpecificDataAsync(nostrPubKeyHex, "angor:project");
            if (!string.IsNullOrEmpty(projectJson))
                ProjectContent = projectJson;

            // Load FAQ
            var faqJson = await _relayService.FetchAppSpecificDataAsync(nostrPubKeyHex, "angor:faq");
            if (!string.IsNullOrEmpty(faqJson))
            {
                try
                {
                    var items = JsonConvert.DeserializeObject<List<FaqJsonItem>>(faqJson);
                    if (items != null)
                    {
                        FaqItems.Clear();
                        foreach (var item in items)
                            FaqItems.Add(new FaqItemViewModel { Question = item.Question ?? "", Answer = item.Answer ?? "" });
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to deserialize FAQ content");
                }
            }

            // Load members
            var membersJson = await _relayService.FetchAppSpecificDataAsync(nostrPubKeyHex, "angor:members");
            if (!string.IsNullOrEmpty(membersJson))
            {
                try
                {
                    var membersObj = JsonConvert.DeserializeObject<MembersJson>(membersJson);
                    if (membersObj?.Pubkeys != null)
                    {
                        Members.Clear();
                        foreach (var pk in membersObj.Pubkeys)
                            Members.Add(pk);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to deserialize members content");
                }
            }

            // Load media
            var mediaJson = await _relayService.FetchAppSpecificDataAsync(nostrPubKeyHex, "angor:media");
            if (!string.IsNullOrEmpty(mediaJson))
            {
                try
                {
                    var items = JsonConvert.DeserializeObject<List<MediaJson>>(mediaJson);
                    if (items != null)
                    {
                        MediaItems.Clear();
                        foreach (var item in items)
                            MediaItems.Add(new MediaItemViewModel { Url = item.Url ?? "", Type = item.Type ?? "image" });
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to deserialize media content");
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "LoadAsync failed for project {ProjectId}", _project.ProjectIdentifier);
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>Save all profile data to Nostr relays using the project's Nostr key.</summary>
    public async Task<bool> SaveAsync()
    {
        if (IsSaving) return false;
        IsSaving = true;

        try
        {
            if (_projectSeedDto == null)
            {
                await LoadProjectSeedDtoAsync();
                if (_projectSeedDto == null)
                {
                    ToastRequested?.Invoke("Failed to load project keys. Please try again.");
                    return false;
                }
            }

            var walletId = new WalletId(_project.OwnerWalletId);
            var metadata = new ProjectMetadata
            {
                Name = ProfileName,
                DisplayName = ProfileDisplayName,
                About = ProfileAbout,
                Picture = ProfilePicture,
                Banner = ProfileBanner,
                Nip05 = ProfileNip05,
                Lud16 = ProfileLud16,
                Website = ProfileWebsite,
            };

            var faqJson = FaqItems.Count > 0
                ? JsonConvert.SerializeObject(FaqItems.Select(f => new FaqJsonItem { Question = f.Question, Answer = f.Answer }))
                : null;

            var membersJson = Members.Count > 0
                ? JsonConvert.SerializeObject(new MembersJson { Pubkeys = Members.ToList() })
                : null;

            var mediaJson = MediaItems.Count > 0
                ? JsonConvert.SerializeObject(MediaItems.Select(m => new MediaJson { Url = m.Url, Type = m.Type }))
                : null;

            var projectContentJson = string.IsNullOrEmpty(ProjectContent) ? null : ProjectContent;

            var result = await _projectAppService.UpdateProjectProfile(
                walletId,
                _projectSeedDto,
                metadata,
                projectContentJson,
                faqJson,
                membersJson,
                mediaJson);

            if (result.IsFailure)
            {
                ToastRequested?.Invoke($"Save failed: {result.Error}");
                return false;
            }

            ToastRequested?.Invoke("Profile saved successfully.");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SaveAsync failed");
            ToastRequested?.Invoke("An unexpected error occurred while saving.");
            return false;
        }
        finally
        {
            IsSaving = false;
        }
    }

    // ── Tab navigation ──
    public void SetActiveTab(string tabId) => ActiveTab = tabId;

    // ── FAQ tab helpers ──
    public void AddFaqItem() => FaqItems.Add(new FaqItemViewModel());

    public void RemoveFaqItem(FaqItemViewModel item)
    {
        if (FaqItems.Count > 1)
            FaqItems.Remove(item);
    }

    // ── Members tab helpers ──
    public void AddMember()
    {
        if (string.IsNullOrWhiteSpace(NewMemberPubKey)) return;
        Members.Add(NewMemberPubKey.Trim());
        NewMemberPubKey = "";
    }

    public void RemoveMember(string pubkey) => Members.Remove(pubkey);

    // ── Media tab helpers ──
    public void AddMedia()
    {
        if (string.IsNullOrWhiteSpace(NewMediaUrl)) return;
        MediaItems.Add(new MediaItemViewModel { Url = NewMediaUrl.Trim(), Type = NewMediaType });
        NewMediaUrl = "";
    }

    public void RemoveMedia(MediaItemViewModel item) => MediaItems.Remove(item);

    // ── Relays tab helpers ──
    public void AddRelay()
    {
        var url = NewRelayUrl.Trim();
        if (string.IsNullOrEmpty(url) || !url.StartsWith("wss://")) return;
        if (!Relays.Contains(url))
            Relays.Add(url);
        NewRelayUrl = "";
    }

    public void RemoveRelay(string url)
    {
        if (Relays.Count > 1)
            Relays.Remove(url);
    }

    // ── Private helpers ──
    private async Task LoadProjectSeedDtoAsync()
    {
        if (_projectSeedDto != null) return;
        if (string.IsNullOrEmpty(_project.ProjectIdentifier)) return;

        try
        {
            var projectResult = await _projectService.GetAsync(new ProjectId(_project.ProjectIdentifier));
            if (projectResult.IsFailure) return;

            var project = projectResult.Value;
            _projectSeedDto = new ProjectSeedDto(
                project.FounderKey ?? "",
                project.FounderRecoveryKey ?? "",
                project.NostrPubKey ?? "",
                _project.ProjectIdentifier);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "LoadProjectSeedDtoAsync failed");
        }
    }

    // ── JSON helper records ──
    private record FaqJsonItem
    {
        [JsonProperty("question")] public string? Question { get; set; }
        [JsonProperty("answer")] public string? Answer { get; set; }
    }

    private record MembersJson
    {
        [JsonProperty("pubkeys")] public List<string>? Pubkeys { get; set; }
    }

    private record MediaJson
    {
        [JsonProperty("url")] public string? Url { get; set; }
        [JsonProperty("type")] public string? Type { get; set; }
    }
}
