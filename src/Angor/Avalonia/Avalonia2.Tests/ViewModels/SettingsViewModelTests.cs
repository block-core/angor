using Angor.Shared;
using Avalonia2.UI.Sections.Settings;

namespace Avalonia2.Tests.ViewModels;

public class SettingsViewModelTests
{
    private readonly Mock<INetworkService> _networkService = new();
    private readonly Mock<INetworkConfiguration> _networkConfig = new();
    private readonly Mock<INetworkStorage> _networkStorage = new();
    private readonly PrototypeSettings _prototypeSettings = new();

    private SettingsViewModel CreateVm()
    {
        _networkStorage.Setup(x => x.GetNetwork()).Returns("Angornet");
        _networkStorage.Setup(x => x.GetSettings()).Returns(new SettingsInfo
        {
            Explorers = new List<SettingsUrl>
            {
                new() { Url = "https://explorer.angor.io", IsPrimary = true }
            },
            Indexers = new List<SettingsUrl>
            {
                new() { Url = "https://indexer.angor.io", IsPrimary = true, Status = UrlStatus.Online }
            },
            Relays = new List<SettingsUrl>
            {
                new() { Url = "wss://relay.angor.io", Name = "Angor Relay", Status = UrlStatus.Online }
            }
        });

        return new SettingsViewModel(
            _networkService.Object,
            _networkConfig.Object,
            _networkStorage.Object,
            _prototypeSettings);
    }

    [Fact]
    public void Constructor_LoadsSettingsFromSdk()
    {
        var vm = CreateVm();

        vm.NetworkType.Should().Be("Angornet");
        vm.ExplorerLinks.Should().HaveCount(1);
        vm.ExplorerLinks[0].Url.Should().Be("https://explorer.angor.io");
        vm.ExplorerLinks[0].IsDefault.Should().BeTrue();
        vm.IndexerLinks.Should().HaveCount(1);
        vm.IndexerLinks[0].Status.Should().Be("Online");
        vm.NostrRelays.Should().HaveCount(1);
        vm.NostrRelays[0].Name.Should().Be("Angor Relay");
    }

    [Fact]
    public void AddExplorerLink_AddsAndSaves()
    {
        var vm = CreateVm();
        vm.NewExplorerUrl = "https://new-explorer.com";

        vm.AddExplorerLink();

        vm.ExplorerLinks.Should().HaveCount(2);
        vm.ExplorerLinks[1].Url.Should().Be("https://new-explorer.com");
        vm.ExplorerLinks[1].IsDefault.Should().BeFalse();
        vm.NewExplorerUrl.Should().BeEmpty();
        _networkStorage.Verify(x => x.SetSettings(It.IsAny<SettingsInfo>()), Times.Once);
    }

    [Fact]
    public void AddExplorerLink_EmptyUrl_DoesNothing()
    {
        var vm = CreateVm();
        vm.NewExplorerUrl = "   ";

        vm.AddExplorerLink();

        vm.ExplorerLinks.Should().HaveCount(1);
    }

    [Fact]
    public void RemoveExplorerLink_RemovesAndSaves()
    {
        var vm = CreateVm();
        var link = vm.ExplorerLinks[0];

        vm.RemoveExplorerLink(link);

        vm.ExplorerLinks.Should().BeEmpty();
        _networkStorage.Verify(x => x.SetSettings(It.IsAny<SettingsInfo>()), Times.Once);
    }

    [Fact]
    public void SetDefaultExplorer_UpdatesDefaults()
    {
        var vm = CreateVm();
        vm.NewExplorerUrl = "https://second.com";
        vm.AddExplorerLink();

        vm.SetDefaultExplorer(vm.ExplorerLinks[1]);

        vm.ExplorerLinks[0].IsDefault.Should().BeFalse();
        vm.ExplorerLinks[1].IsDefault.Should().BeTrue();
    }

    [Fact]
    public void AddIndexerLink_AddsAndSaves()
    {
        var vm = CreateVm();
        vm.NewIndexerUrl = "https://new-indexer.com";

        vm.AddIndexerLink();

        vm.IndexerLinks.Should().HaveCount(2);
        vm.IndexerLinks[1].Url.Should().Be("https://new-indexer.com");
        vm.IndexerLinks[1].Status.Should().Be("Offline");
    }

    [Fact]
    public void AddRelayLink_AddsAndSaves()
    {
        var vm = CreateVm();
        vm.NewRelayUrl = "wss://custom-relay.com";

        vm.AddRelayLink();

        vm.NostrRelays.Should().HaveCount(2);
        vm.NostrRelays[1].Url.Should().Be("wss://custom-relay.com");
        vm.NostrRelays[1].Name.Should().Be("Custom");
        vm.NostrRelays[1].Status.Should().Be("Online");
    }

    [Fact]
    public void ConfirmNetworkSwitch_SameNetwork_DoesNothing()
    {
        var vm = CreateVm();
        vm.OpenNetworkModal();
        vm.SelectNetworkOption("Angornet");
        vm.NetworkChangeConfirmed = true;

        vm.ConfirmNetworkSwitch();

        vm.NetworkType.Should().Be("Angornet");
        _networkStorage.Verify(x => x.SetNetwork(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public void ConfirmNetworkSwitch_DifferentNetwork_SwitchesAndReloads()
    {
        var vm = CreateVm();
        vm.OpenNetworkModal();
        vm.SelectNetworkOption("Testnet");
        vm.NetworkChangeConfirmed = true;

        // Setup for reload after switch
        _networkStorage.Setup(x => x.GetSettings()).Returns(new SettingsInfo());

        vm.ConfirmNetworkSwitch();

        vm.NetworkType.Should().Be("Testnet");
        _networkStorage.Verify(x => x.SetNetwork("Testnet"), Times.Once);
        _networkStorage.Verify(x => x.SetSettings(It.IsAny<SettingsInfo>()), Times.AtLeastOnce);
        _networkService.Verify(x => x.AddSettingsIfNotExist(), Times.AtLeast(2)); // once in ctor, once in switch
    }

    [Fact]
    public void ConfirmNetworkSwitch_WithoutConfirmation_DoesNothing()
    {
        var vm = CreateVm();
        vm.OpenNetworkModal();
        vm.SelectNetworkOption("Mainnet");
        // NetworkChangeConfirmed not set

        vm.ConfirmNetworkSwitch();

        vm.NetworkType.Should().Be("Angornet");
    }

    [Fact]
    public void ConfirmWipeData_ClearsAndReinitializes()
    {
        var vm = CreateVm();

        // Setup for reload after wipe
        _networkStorage.Setup(x => x.GetSettings()).Returns(new SettingsInfo());

        vm.ConfirmWipeData();

        _networkStorage.Verify(x => x.SetSettings(It.Is<SettingsInfo>(s =>
            s.Explorers.Count == 0 && s.Indexers.Count == 0 && s.Relays.Count == 0)), Times.Once);
        _networkService.Verify(x => x.AddSettingsIfNotExist(), Times.AtLeast(2));
        vm.IsWipeDataModalOpen.Should().BeFalse();
    }

    [Fact]
    public void ShowPopulatedApp_DelegatesToPrototypeSettings()
    {
        var vm = CreateVm();

        vm.ShowPopulatedApp.Should().BeTrue(); // default

        vm.ShowPopulatedApp = false;

        _prototypeSettings.ShowPopulatedApp.Should().BeFalse();
    }

    [Fact]
    public void OpenNetworkModal_SetsState()
    {
        var vm = CreateVm();

        vm.OpenNetworkModal();

        vm.IsNetworkModalOpen.Should().BeTrue();
        vm.SelectedNetworkToSwitch.Should().Be("Angornet");
        vm.NetworkChangeConfirmed.Should().BeFalse();
    }

    [Fact]
    public void CloseNetworkModal_ClearsState()
    {
        var vm = CreateVm();
        vm.OpenNetworkModal();

        vm.CloseNetworkModal();

        vm.IsNetworkModalOpen.Should().BeFalse();
    }

    [Fact]
    public void CurrencyDisplay_DefaultIsBtc()
    {
        var vm = CreateVm();

        vm.CurrencyDisplay.Should().Be("BTC");
    }
}
