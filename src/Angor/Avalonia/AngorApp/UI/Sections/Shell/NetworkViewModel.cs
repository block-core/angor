using Angor.Shared;
using Blockcore.Networks;

namespace AngorApp.UI.Sections.Shell;

public class NetworkViewModel : INetworkViewModel
{
    public NetworkViewModel(Blockcore.Networks.Network network, INetworkConfiguration networkConfiguration)
    {
        Name = network.Name;
        NetworkType = network.NetworkType;
        IsDebugMode = networkConfiguration.GetDebugMode();
    }

    public NetworkType NetworkType { get; }

    public string Name { get; set; }

    public bool IsDebugMode { get; }
}

internal interface INetworkViewModel
{
  public NetworkType NetworkType { get; }
    public string Name { get; set; }
    public bool IsDebugMode { get; }
}

internal class NetworkViewModelSample : INetworkViewModel
{
    public NetworkType NetworkType { get; } = NetworkType.Mainnet;
    public string Name { get; set; } = "Main";
    public bool IsDebugMode { get; } = true;
}