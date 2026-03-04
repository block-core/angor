using AngorApp.Model.Funded.Shared.Model;

namespace AngorApp.UI.Sections.Funded.Shared.Manage;

public class ManageViewModel(IFunded funded) : IManageViewModel
{
    public IFunded Funded { get; } = funded;
}