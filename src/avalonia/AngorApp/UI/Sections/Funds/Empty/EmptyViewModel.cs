using AngorApp.UI.Flows.AddWallet;
using Zafiro.Reactive;

namespace AngorApp.UI.Sections.Funds.Empty
{
    public class EmptyViewModel : IEmptyViewModel
    {
        public EmptyViewModel(IAddWalletFlow addWalletFlow)
        {
            AddWallet = EnhancedCommand.Create(async () => await addWalletFlow.Run());
        }

        public IEnhancedCommand AddWallet { get; }
    }
}
