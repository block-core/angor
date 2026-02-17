using AngorApp.UI.Sections.Funders.Chat;

namespace AngorApp;

public class ChatViewModelSample : IChatViewModel
{
    public ChatViewModelSample()
    {
        var sampleMessage = """
                            {
                              "projectIdentifier": 1,
                              "unfundedReleaseAddress": "bc1qxy2kgdygjrsqtzq2n0yrf2493p83kkfjhx0wlh",
                              "investmentTransactionHex": "01000000016cd07ed043c1bdd3bb3712ee2816900f4f0546ddf1bb6b6fddee20381485d0200000000000ffffffff061027000000000000160014803943e123818e23600673ed9af7d00000000"
                            }
                            """;
        Messages =
        [
            new ChatMessage(sampleMessage, false),
            new ChatMessage("Hola", true)
        ];
    }

    public IEnumerable<ChatMessage> Messages { get; }

    public string CurrentText { get; set; } = "";
    public IEnhancedCommand SendMessage { get; } = EnhancedCommand.Create(() => { });
}