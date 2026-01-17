using ReactiveUI.Validation.Abstractions;

namespace AngorApp.UI.Flows.CreateProject.Wizard
{

    public interface IProjectProfile : IValidatableViewModel, IValidatable, System.ComponentModel.INotifyDataErrorInfo
    {

        string Name { get; set; }
        string Description { get; set; }
        string Website { get; set; }


        string AvatarUri { get; set; }
        string BannerUri { get; set; }
        string Nip05 { get; set; }
        string Lud16 { get; set; }
        string Nip57 { get; set; }
    }
}
