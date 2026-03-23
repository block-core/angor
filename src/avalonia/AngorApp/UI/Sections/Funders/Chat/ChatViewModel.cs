using DynamicData;

namespace AngorApp.UI.Sections.Funders.Chat;

public partial class ChatViewModel : ReactiveObject, IHaveTitle, IChatViewModel
{
    [Reactive] private string currentText = "";

    public ChatViewModel(string destinationNpub)
    {
        SourceCache<ChatMessage, string> messageSource = new(message => message.Id);
        messageSource.Edit(updater => updater.Load(
        [
            new ChatMessage("This is some sample text", false),
            new ChatMessage("This is some sample text2", false),
            new ChatMessage("This is some sample text3", false),
        ]));
            
        messageSource.Connect()
                     .Bind(out var messages)
                     .Subscribe();

        Title = Observable.Return($"{destinationNpub}");
        Messages = messages;
            
        SendMessage = EnhancedCommand.Create(() =>
        {
            messageSource.AddOrUpdate(new ChatMessage(CurrentText, true));
            CurrentText = "";
        }, this.WhenAnyValue(model => model.CurrentText, x => !string.IsNullOrWhiteSpace(x)));
    }
        
    public IObservable<string> Title { get; }
    public IEnumerable<ChatMessage> Messages { get; }
    public IEnhancedCommand SendMessage { get; }
}