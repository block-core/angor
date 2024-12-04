using System;
using System.Collections.Generic;
using System.Reactive;
using ReactiveUI;

namespace AngorApp.Sections.Browse;

public class BrowseViewModelDesign : IBrowseViewModel
{
    public BrowseViewModelDesign()
    {
        Projects =
        [
            new Project("Ariton") { Picture = new Uri("https://ariton.app/assets/ariton-social.png") , ShortDescription = "Community Super App"},
            new Project("Matrix 5") { Picture = new Uri("https://m.primal.net/KrhZ.jpg"), ShortDescription = "Matrix 5 Project" },
            new Project("Bitcoin festival")  { Picture = new Uri("https://unchainedcrypto.com/wp-content/uploads/2023/10/bitcoin-hashrate.jpg") },
        ];
    }
    
    public IReadOnlyCollection<Project> Projects { get; set; }
    public ReactiveCommand<Unit, Unit> OpenHub { get; set; }
}