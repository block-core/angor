using AngorApp.UI.Sections.MyProjects.ManageFunds.Release.ReleaseDialog;
using AngorApp.UI.Shared.OperationResult;
using Zafiro.Avalonia.Dialogs;
using Zafiro.Reactive;
using Option = Zafiro.Avalonia.Dialogs.Option;

namespace AngorApp.UI.Sections.MyProjects.ManageFunds.Release
{
    public class ReleaseViewModel : IReleaseViewModel
    {
        private readonly UIServices uiServices;

        public ReleaseViewModel(IFullProject project, UIServices uiServices)
        {
            this.uiServices = uiServices;
            Project = project;
            ReleaseAll = GetReleaseCommand();
        }

        public IFullProject Project { get; }

        public IEnhancedCommand ReleaseAll { get; }

        private IEnhancedCommand<Unit> GetReleaseCommand()
        {
            return EnhancedCommand.Create(() => uiServices.Dialog.Show(
                                              new ReleaseDialogViewModel(),
                                              "Release funds",
                                              (model, closeable) =>
                                              {
                                                  IEnhancedCommand releaseCommand =
                                                      EnhancedCommand.Create(() => ReleaseFunds()
                                                                                 .Tap(() => NotifySuccess(
                                                                                     model,
                                                                                     closeable)));
                                                  IEnumerable<IOption> options =
                                                  [
                                                      new Option(
                                                          "Cancel",
                                                          EnhancedCommand.Create(
                                                              closeable.Close,
                                                              releaseCommand.IsExecuting.Not()),
                                                          new Zafiro.Avalonia.Dialogs.Settings
                                                          {
                                                              IsCancel = true, Role = OptionRole.Secondary
                                                          }),
                                                      new Option(
                                                          releaseCommand.IsExecuting.Select(b => b
                                                              ? "Releasing..."
                                                              : "Release Funds"),
                                                          releaseCommand,
                                                          new Zafiro.Avalonia.Dialogs.Settings())
                                                  ];

                                                  return options;
                                              }));
        }

        private async Task NotifySuccess(ReleaseDialogViewModel model, ICloseable closeable)
        {
            closeable.Close();
            IAmountUI amount = new AmountUI(model.Selection.SelectedItems.Sum(x => x.Amount.Sats));
            await uiServices.Dialog.Show(
                new OperationResultViewModel(
                    "Funds Returned",
                    $"{amount.BtcString} has been successfully returned to investors",
                    new Icon("fa-check")),
                "",
                (_, innerCloseable) =>
                [
                    new Option(
                        "Done",
                        EnhancedCommand.Create(innerCloseable.Close),
                        new Zafiro.Avalonia.Dialogs.Settings { IsCancel = true, Role = OptionRole.Primary })
                ]);
            await uiServices.NotificationService.Show("Funds released successfully!", "Release Funds");
        }

        private static async Task<Result> ReleaseFunds()
        {
            // TODO:
            await Task.Delay(4000);
            return await Task.FromResult(Result.Success());
        }
    }
}