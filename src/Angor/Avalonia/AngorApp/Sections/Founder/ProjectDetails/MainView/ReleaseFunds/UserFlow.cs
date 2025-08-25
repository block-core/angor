using System.Threading.Tasks;
using AngorApp.UI.Services;
using Zafiro.Avalonia.Dialogs;

namespace AngorApp.Sections.Founder.ProjectDetails.MainView.ReleaseFunds;

public abstract class UserFlow
{
    public static Task<Maybe<Result>> PromptAndNotify(Func<Task<Result>> action, UIServices uiServices, string promptMessage, string promptTitle, string successMessage, string successTitle = "Success", Func<string, string>? getErrorMessage = null, string? errorTitle = "Error")
    {
        getErrorMessage ??= e => $"Error {e}";
        
        return uiServices.Dialog.ShowConfirmation(promptTitle, promptMessage)
            .Where(b => b)
            .Map(_ => action()
                .Tap(() => uiServices.Dialog.ShowMessage(successMessage, successTitle))
                .TapError(e => uiServices.NotificationService.Show(getErrorMessage(e), errorTitle)));
    }
}