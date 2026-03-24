using Angor.Shared;
using AngorApp.UI.Flows.CreateProject.Wizard.InvestmentProject;
using AngorApp.UI.Shared.Controls.ImagePicker;
using AngorApp.UI.Shared.Services.Blossom;
using ReactiveUI.Validation.Extensions;
using Zafiro.Avalonia.Dialogs;

namespace AngorApp.UI.Flows.CreateProject
{
    internal class ImagePicker : IImagePicker
    {
        private readonly UIServices uiServices;
        private readonly IBlossomService blossomService;
        private readonly INetworkStorage networkStorage;

        public ImagePicker(UIServices uiServices, IBlossomService blossomService, INetworkStorage networkStorage)
        {
            this.uiServices = uiServices;
            this.blossomService = blossomService;
            this.networkStorage = networkStorage;
        }
        
        public async Task<Maybe<Uri>> PickImage()
        {
            return await uiServices.Dialog.ShowAndGetResult(new ImagePickerViewModel(uiServices, blossomService, networkStorage), "Pick Image", model => model.IsValid(), model => model.ImageUri)
                            .Map(s => new Uri(s!, UriKind.Absolute));
        }
    }
}