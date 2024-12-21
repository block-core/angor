using System.Linq;
using AngorApp.Model;

namespace AngorApp.Sections.Browse;

public static class SampleData 
{
    public static IEnumerable<IProject> GetProjects()
    {
        IEnumerable<ProjectDesign> projects =
        [
            new ProjectDesign()
            {
                Name = "GHJ-TGH-56",
                Picture = new Uri("https://img.freepik.com/premium-photo/shoot-blue-nebula-with-purple-parts-deep-space-colorful-generative-ai-aig15_31965-139737.jpg"),
                Icon = new Uri("https://img.freepik.com/premium-photo/shoot-blue-nebula-with-purple-parts-deep-space-colorful-generative-ai-aig15_31965-139737.jpg"),
                ShortDescription = "Short Description",
                Id = "Id"
            },
            new()
            {
                Name = "Visit a black hole",
                Picture = new Uri("https://img.freepik.com/free-photo/magical-fantasy-black-hole-illustration_23-2151678388.jpg?t=st=1733787946~exp=1733791546~hmac=0b93c5bbb4a6e522b8e61dc3f72bf4e7c2714f5f40d4b982493011cca8a9d97e&w=1380"),
                Icon = new Uri("https://img.freepik.com/free-photo/magical-fantasy-black-hole-illustration_23-2151678388.jpg?t=st=1733787946~exp=1733791546~hmac=0b93c5bbb4a6e522b8e61dc3f72bf4e7c2714f5f40d4b982493011cca8a9d97e&w=1380")
            },
            new()
            {
                Name = "Making investments great again",
                ShortDescription = "Decentralize everything",
                Picture = new Uri("https://img.freepik.com/free-photo/minimalistic-still-life-assortment-with-cryptocurrency_23-2149102095.jpg?t=st=1733788307~exp=1733791907~hmac=baf06e4f989aa3bdf3cf1a94c44ad9f72b4595e3a897b9257cb26a8b405e4a00&w=1380"),
                Icon = new Uri("https://img.freepik.com/free-vector/digital-bitcoin-currency-symbol-vector-design_1017-10540.jpg?t=st=1733788336~exp=1733791936~hmac=4e10d9eaff66dfa94ca098207656c448b64369c61fb073438c4361e2c32e9f33&w=826")
            },
            new()
            {
                Name = "Space exploration",
                Picture = new Uri("https://images.pexels.com/photos/998641/pexels-photo-998641.jpeg?auto=compress&cs=tinysrgb&w=600"),
                Icon = new Uri("https://images.pexels.com/photos/998641/pexels-photo-998641.jpeg?auto=compress&cs=tinysrgb&w=600")
            },
            new()
            {
                Name = "Ariton",
                Picture = new Uri("https://ariton.app/assets/ariton-social.png"),
                ShortDescription = "Community Super App",
                Icon = new Uri("https://ariton.app/assets/community.webp")
            },
            new()
            {
                Name = "Matrix 5",
                Picture = new Uri("https://m.primal.net/KrhZ.jpg"),
                ShortDescription = "Matrix 5 Project",
                Icon = new Uri("https://pfp.nostr.build/5828e07a01a89d6059e85a00ca57680a1b835f2ad197afb2798ad8c7e175cf65.jpg")
            },
            new()
            {
                Name = "Bitcoin festival",
                Picture = new Uri("https://unchainedcrypto.com/wp-content/uploads/2023/10/bitcoin-hashrate.jpg"),
                Icon = new Uri("https://unchainedcrypto.com/wp-content/uploads/2023/10/bitcoin-hashrate.jpg")
            },
        ];

        return projects;
    }

    public static string TestNetBitcoinAddress { get; } = "mzHrLAR3WWLE4eCpq82BDCKmLeYRyYXPtm";
}