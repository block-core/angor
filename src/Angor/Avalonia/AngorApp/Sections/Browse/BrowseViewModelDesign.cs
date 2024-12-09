using System.Linq;
using Zafiro.Avalonia.Controls.Navigation;

namespace AngorApp.Sections.Browse;

public class BrowseViewModelDesign : IBrowseViewModel
{
    public BrowseViewModelDesign()
    {
        IEnumerable<Project> projectModels =
        [
            new Project("Space exploration")
            {
                Picture = new Uri("https://images.pexels.com/photos/998641/pexels-photo-998641.jpeg?auto=compress&cs=tinysrgb&w=600"),
                Icon = new Uri("https://images.pexels.com/photos/998641/pexels-photo-998641.jpeg?auto=compress&cs=tinysrgb&w=600")
            },
            new Project("Ariton")
            {
                Picture = new Uri("https://ariton.app/assets/ariton-social.png"),
                ShortDescription = "Community Super App",
                Icon = new Uri("https://ariton.app/assets/community.webp")
            },
            new Project("Matrix 5")
            {
                Picture = new Uri("https://m.primal.net/KrhZ.jpg"),
                ShortDescription = "Matrix 5 Project",
                Icon = new Uri("https://pfp.nostr.build/5828e07a01a89d6059e85a00ca57680a1b835f2ad197afb2798ad8c7e175cf65.jpg")
            },
            new Project("Bitcoin festival")
            {
                Picture = new Uri("https://unchainedcrypto.com/wp-content/uploads/2023/10/bitcoin-hashrate.jpg"),
                Icon = new Uri("https://unchainedcrypto.com/wp-content/uploads/2023/10/bitcoin-hashrate.jpg")
            },
        ];
        
        Projects = projectModels.Select(project => new ProjectViewModel(project, null)).ToList();
    }
    
    public IReadOnlyCollection<ProjectViewModel> Projects { get; set; }
    public ReactiveCommand<Unit, Unit> OpenHub { get; set; }
}