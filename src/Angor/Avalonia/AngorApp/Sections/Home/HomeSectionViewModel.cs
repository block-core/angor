using AngorApp.Sections.Wallet.NoWallet;

namespace AngorApp.Sections.Home;

public class HomeSectionViewModel : ReactiveObject, IHomeSectionViewModel
{
    private readonly IWalletProvider provider;

    public HomeSectionViewModel(IWalletProvider provider)
    {
        this.provider = provider;
        provider.GetWallet();
    }

    public bool IsWalletSetup => provider.GetWallet().HasValue;
}

public interface IHomeSectionViewModel
{
    public bool IsWalletSetup { get; }
}