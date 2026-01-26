using AngorApp.UI.Flows.CreateProject.Wizard.InvestmentProject;
using AngorApp.UI.Shared.Controls.ImagePicker;
using ReactiveUI.Validation.Extensions;
using Zafiro.Avalonia.Dialogs;

namespace AngorApp.UI.Flows.CreateProject
{
    internal class ImagePicker : IImagePicker
    {
        private readonly UIServices uiServices;

        public ImagePicker(UIServices uiServices)
        {
            this.uiServices = uiServices;
        }
        
        public async Task<Maybe<Uri>> PickImage()
        {
            return await uiServices.Dialog.ShowAndGetResult(new ImagePickerViewModel(uiServices), "Pick Image", model => model.IsValid(), model => model.ImageUri)
                            .Map(s => new Uri(s!, UriKind.Absolute));
        }
    }
}