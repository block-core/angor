using System.Collections.ObjectModel;
using System.Reactive.Linq;
using Angor.Sdk.Common;
using Angor.Sdk.Funding.Projects;
using Angor.Sdk.Funding.Projects.Operations;
using Angor.Sdk.Funding.Shared;
using Angor.Shared.Models;
using App.UI.Shared.Helpers;
using Avalonia.Media.Imaging;
using Microsoft.Extensions.Logging;
using ReactiveUI;
using ReactiveUI.SourceGenerators;

namespace App.UI.Sections.MyProjects.EditProfile;

/// <summary>
/// A single FAQ item for the edit profile FAQ tab.
/// </summary>
public partial class FaqItemViewModel : ReactiveObject
{
    [Reactive] private string question = "";
    [Reactive] private string answer = "";
}

/// <summary>
/// A single media item (image or video URL) for the edit profile Media tab.
/// </summary>
public partial class MediaItemViewModel : ReactiveObject
{
    [Reactive] private string url = "";
    [Reactive] private string type = "image";
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
    private readonly ILogger<EditProfileViewModel> _logger;

    public event Action<string>? ToastRequested;

    // ── Active tab ──
    [Reactive] private string activeTab = "profile";

    // ── Loading / Saving state ──
    [Reactive] private bool isLoading;
    [Reactive] private bool isSaving;

    // ── Profile tab ──
    [Reactive] private string profileName = "";
    [Reactive] private string profileDisplayName = "";
    [Reactive] private string profileAbout = "";
    [Reactive] private string profilePicture = "";
    [Reactive] private string profileBanner = "";
    [Reactive] private Bitmap? profilePictureBitmap;
    [Reactive] private Bitmap? profileBannerBitmap;
    [Reactive] private string profileNip05 = "";
    [Reactive] private string profileLud16 = "";
    [Reactive] private string profileWebsite = "";

    // ── Project tab ──
    [Reactive] private string projectContent = "";

    // ── Members tab ──
    [Reactive] private string newMemberPubKey = "";

    // ── Media tab ──
    [Reactive] private string newMediaUrl = "";
    [Reactive] private string newMediaType = "image";

    // ── Relays tab ──
    [Reactive] private string newRelayUrl = "";

    // ── Validation errors ──
    [Reactive] private string memberError = "";
    [Reactive] private string mediaError = "";
    [Reactive] private string nip05Error = "";
    [Reactive] private string lud16Error = "";
    [Reactive] private string websiteError = "";

    public bool HasMemberError => !string.IsNullOrEmpty(MemberError);
    public bool HasMediaError => !string.IsNullOrEmpty(MediaError);
    public bool HasNip05Error => !string.IsNullOrEmpty(Nip05Error);
    public bool HasLud16Error => !string.IsNullOrEmpty(Lud16Error);
    public bool HasWebsiteError => !string.IsNullOrEmpty(WebsiteError);

    // ── Collections ──
    public ObservableCollection<FaqItemViewModel> FaqItems { get; } = new();
    public ObservableCollection<string> Members { get; } = new();
    public ObservableCollection<MediaItemViewModel> MediaItems { get; } = new();
    public ObservableCollection<string> Relays { get; } = new();

    public string ProjectName => _project.Name;

    public EditProfileViewModel(
        MyProjectItemViewModel project,
        IProjectAppService projectAppService,
        ILogger<EditProfileViewModel> logger)
    {
        _project = project;
        _projectAppService = projectAppService;
        _logger = logger;

        // Add one empty FAQ item to start
        FaqItems.Add(new FaqItemViewModel());

        // Load image previews when URLs change
        this.WhenAnyValue(x => x.ProfilePicture)
            .Throttle(TimeSpan.FromMilliseconds(500))
            .ObserveOn(RxSchedulers.MainThreadScheduler)
            .Subscribe(url => ImageCacheService.LoadBitmapAsync(url, bmp => ProfilePictureBitmap = bmp, decodeWidth: 256));

        this.WhenAnyValue(x => x.ProfileBanner)
            .Throttle(TimeSpan.FromMilliseconds(500))
            .ObserveOn(RxSchedulers.MainThreadScheduler)
            .Subscribe(url => ImageCacheService.LoadBitmapAsync(url, bmp => ProfileBannerBitmap = bmp, decodeWidth: 1280));

        // Re-raise the computed Has*Error flags whenever the backing error text changes.
        this.WhenAnyValue(x => x.MemberError).Subscribe(_ => this.RaisePropertyChanged(nameof(HasMemberError)));
        this.WhenAnyValue(x => x.MediaError).Subscribe(_ => this.RaisePropertyChanged(nameof(HasMediaError)));
        this.WhenAnyValue(x => x.Nip05Error).Subscribe(_ => this.RaisePropertyChanged(nameof(HasNip05Error)));
        this.WhenAnyValue(x => x.Lud16Error).Subscribe(_ => this.RaisePropertyChanged(nameof(HasLud16Error)));
        this.WhenAnyValue(x => x.WebsiteError).Subscribe(_ => this.RaisePropertyChanged(nameof(HasWebsiteError)));

        // Clear field errors as soon as the user edits the offending input.
        this.WhenAnyValue(x => x.NewMemberPubKey).Skip(1).Subscribe(_ => MemberError = "");
        this.WhenAnyValue(x => x.NewMediaUrl).Skip(1).Subscribe(_ => MediaError = "");
        this.WhenAnyValue(x => x.ProfileNip05).Skip(1).Subscribe(_ => Nip05Error = "");
        this.WhenAnyValue(x => x.ProfileLud16).Skip(1).Subscribe(_ => Lud16Error = "");
        this.WhenAnyValue(x => x.ProfileWebsite).Skip(1).Subscribe(_ => WebsiteError = "");

        _ = LoadAsync();
    }

    /// <summary>Load existing profile and content data from Nostr relays.</summary>
    public async Task LoadAsync()
    {
        if (IsLoading) return;
        IsLoading = true;

        try
        {
            var projectId = new ProjectId(_project.ProjectIdentifier);
            var profileResult = await _projectAppService.FetchProjectProfileData(projectId);
            if (profileResult.IsFailure)
            {
                _logger.LogWarning("Failed to load profile for project {ProjectId}: {Error}",
                    _project.ProjectIdentifier, profileResult.Error);
                return;
            }

            var data = profileResult.Value;

            if (data.Metadata != null)
            {
                ProfileName = data.Metadata.Name ?? "";
                ProfileDisplayName = data.Metadata.DisplayName ?? "";
                ProfileAbout = data.Metadata.About ?? "";
                ProfilePicture = data.Metadata.Picture ?? "";
                ProfileBanner = data.Metadata.Banner ?? "";
                ProfileNip05 = data.Metadata.Nip05 ?? "";
                ProfileLud16 = data.Metadata.Lud16 ?? "";
                ProfileWebsite = data.Metadata.Website ?? "";
            }

            if (!string.IsNullOrEmpty(data.ProjectContent))
                ProjectContent = data.ProjectContent;

            if (data.FaqItems != null)
            {
                FaqItems.Clear();
                foreach (var item in data.FaqItems)
                    FaqItems.Add(new FaqItemViewModel { Question = item.Question ?? "", Answer = item.Answer ?? "" });
                if (FaqItems.Count == 0)
                    FaqItems.Add(new FaqItemViewModel());
            }

            if (data.MemberPubkeys != null)
            {
                Members.Clear();
                foreach (var pk in data.MemberPubkeys)
                    Members.Add(pk);
            }

            if (data.MediaItems != null)
            {
                MediaItems.Clear();
                foreach (var item in data.MediaItems)
                    MediaItems.Add(new MediaItemViewModel { Url = item.Url ?? "", Type = item.Type ?? "image" });
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
            // Validate the free-form profile fields before publishing to relays.
            if (!ValidateProfileFields())
            {
                ToastRequested?.Invoke("Please fix the highlighted fields before saving.");
                return false;
            }

            var walletId = new WalletId(_project.OwnerWalletId);
            var projectId = new ProjectId(_project.ProjectIdentifier);
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

            var faqItems = FaqItems
                .Select(f => new FaqItem { Question = f.Question, Answer = f.Answer })
                .ToList();

            var mediaItems = MediaItems
                .Select(m => new MediaItem { Url = m.Url, Type = m.Type })
                .ToList();

            var result = await _projectAppService.UpdateProjectProfile(
                walletId,
                projectId,
                metadata,
                string.IsNullOrEmpty(ProjectContent) ? null : ProjectContent,
                faqItems.Count > 0 ? faqItems : null,
                Members.Count > 0 ? Members.ToList() : null,
                mediaItems.Count > 0 ? mediaItems : null);

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
        var pubkey = NewMemberPubKey.Trim();
        if (string.IsNullOrWhiteSpace(pubkey))
        {
            MemberError = "Enter a public key.";
            return;
        }
        if (!IsValidNostrPubKey(pubkey))
        {
            MemberError = "Invalid key — expected an npub1… or 64-character hex public key.";
            return;
        }
        if (Members.Contains(pubkey))
        {
            MemberError = "This member is already added.";
            return;
        }
        Members.Add(pubkey);
        NewMemberPubKey = "";
    }

    public void RemoveMember(string pubkey) => Members.Remove(pubkey);

    // ── Media tab helpers ──
    public void AddMedia()
    {
        var url = NewMediaUrl.Trim();
        if (string.IsNullOrWhiteSpace(url))
        {
            MediaError = "Enter a media URL.";
            return;
        }
        if (!IsValidHttpUrl(url))
        {
            MediaError = "Invalid URL — must start with http:// or https://.";
            return;
        }
        MediaItems.Add(new MediaItemViewModel { Url = url, Type = NewMediaType });
        NewMediaUrl = "";
    }

    /// <summary>Add an already-uploaded media URL (from the Blossom uploader) to the gallery.</summary>
    public void AddUploadedMedia(string url, string type)
    {
        if (string.IsNullOrWhiteSpace(url)) return;
        MediaItems.Add(new MediaItemViewModel { Url = url.Trim(), Type = string.IsNullOrEmpty(type) ? "image" : type });
    }

    public void RemoveMedia(MediaItemViewModel item) => MediaItems.Remove(item);

    // ── Validation helpers ──

    /// <summary>True for a 64-char hex key or an npub1… bech32 key (lightweight UI check).</summary>
    private static bool IsValidNostrPubKey(string value)
    {
        if (System.Text.RegularExpressions.Regex.IsMatch(value, "^[0-9a-fA-F]{64}$"))
            return true;
        return System.Text.RegularExpressions.Regex.IsMatch(value, "^npub1[a-z0-9]{58,}$");
    }

    /// <summary>True for an absolute http(s) URL.</summary>
    private static bool IsValidHttpUrl(string value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri) &&
        (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);

    /// <summary>True for a name@domain.tld style address (NIP-05 / Lightning).</summary>
    private static bool IsValidAddress(string value) =>
        System.Text.RegularExpressions.Regex.IsMatch(value, @"^[^@\s]+@[^@\s]+\.[^@\s]+$");

    /// <summary>Validate the optional NIP-05, Lightning and Website fields; populates *Error props.</summary>
    private bool ValidateProfileFields()
    {
        Nip05Error = "";
        Lud16Error = "";
        WebsiteError = "";
        var ok = true;

        if (!string.IsNullOrWhiteSpace(ProfileNip05) && !IsValidAddress(ProfileNip05.Trim()))
        {
            Nip05Error = "Invalid NIP-05 — use name@domain.";
            ok = false;
        }
        if (!string.IsNullOrWhiteSpace(ProfileLud16) && !IsValidAddress(ProfileLud16.Trim()))
        {
            Lud16Error = "Invalid Lightning address — use name@domain.";
            ok = false;
        }
        if (!string.IsNullOrWhiteSpace(ProfileWebsite) && !IsValidHttpUrl(ProfileWebsite.Trim()))
        {
            WebsiteError = "Invalid URL — must start with http:// or https://.";
            ok = false;
        }

        return ok;
    }

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
}
