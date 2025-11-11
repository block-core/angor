using Blockcore.Networks;

namespace AngorApp.Sections.Shell;

public class NetworkViewModel : INetworkViewModel
{
    public NetworkViewModel(Blockcore.Networks.Network network)
    {
        Name = network.Name;
        NetworkType = network.NetworkType;
    }

    public NetworkType NetworkType { get; }

    public string Name { get; set; }
}

internal interface INetworkViewModel
{
    public NetworkType NetworkType { get; }
    public string Name { get; set; }
}

internal class NetworkViewModelSample : INetworkViewModel
{
    public NetworkType NetworkType { get; } = NetworkType.Mainnet;
    public string Name { get; set; } = "Main";
}