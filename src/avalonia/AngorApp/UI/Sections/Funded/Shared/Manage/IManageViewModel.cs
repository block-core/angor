using AngorApp.Model.Funded.Shared.Model;

namespace AngorApp.UI.Sections.Funded.Shared.Manage
{
    public interface IManageViewModel
    {
        public IFunded Funded { get; }
    }
}