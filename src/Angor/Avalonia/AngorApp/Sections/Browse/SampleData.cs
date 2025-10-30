using System.Linq;

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
                Banner = new Uri("https://img.freepik.com/premium-photo/shoot-blue-nebula-with-purple-parts-deep-space-colorful-generative-ai-aig15_31965-139737.jpg"),
                Picture = new Uri("https://img.freepik.com/premium-photo/shoot-blue-nebula-with-purple-parts-deep-space-colorful-generative-ai-aig15_31965-139737.jpg"),
                ShortDescription = "This is a very big description that came out of my mind. Its purpose is to see how the layout behaves when content is too be to fit the item container",
                Id = "angor1qmd8kazm8uzk7s0gddf4amjx4mzj3n5wzgn3mde"
            },
            new()
            {
                Name = "Visit a black hole",
                Banner = new Uri("https://img.freepik.com/free-photo/magical-fantasy-black-hole-illustration_23-2151678388.jpg?t=st=1733787946~exp=1733791546~hmac=0b93c5bbb4a6e522b8e61dc3f72bf4e7c2714f5f40d4b982493011cca8a9d97e&w=1380"),
                Picture = new Uri("https://img.freepik.com/free-photo/magical-fantasy-black-hole-illustration_23-2151678388.jpg?t=st=1733787946~exp=1733791546~hmac=0b93c5bbb4a6e522b8e61dc3f72bf4e7c2714f5f40d4b982493011cca8a9d97e&w=1380"),
                Id = "angor1qmd8kazm8uzk7s0gddf4amjx4mzj3n5wzgn3mde"
            },
            new()
            {
                Name = "Making investments great again",
                ShortDescription = "Decentralize everything",
                Banner = new Uri("https://img.freepik.com/free-photo/minimalistic-still-life-assortment-with-cryptocurrency_23-2149102095.jpg?t=st=1733788307~exp=1733791907~hmac=baf06e4f989aa3bdf3cf1a94c44ad9f72b4595e3a897b9257cb26a8b405e4a00&w=1380"),
                Picture = new Uri("https://img.freepik.com/free-vector/digital-bitcoin-currency-symbol-vector-design_1017-10540.jpg?t=st=1733788336~exp=1733791936~hmac=4e10d9eaff66dfa94ca098207656c448b64369c61fb073438c4361e2c32e9f33&w=826"),
                Id = "angor1qmd8kazm8uzk7s0gddf4amjx4mzj3n5wzgn3mde"
            },
            new()
            {
                Name = "Space exploration",
                Banner = new Uri("https://images.pexels.com/photos/998641/pexels-photo-998641.jpeg?auto=compress&cs=tinysrgb&w=600"),
                Picture = new Uri("https://images.pexels.com/photos/998641/pexels-photo-998641.jpeg?auto=compress&cs=tinysrgb&w=600"),
                Id = "angor1qmd8kazm8uzk7s0gddf4amjx4mzj3n5wzgn3mde"
            },
            new()
            {
                Name = "Ariton",
                Banner = new Uri("https://ariton.app/assets/ariton-social.png"),
                ShortDescription = "Community Super App",
                Picture = new Uri("https://ariton.app/assets/community.webp"),
                Id = "angor1qmd8kazm8uzk7s0gddf4amjx4mzj3n5wzgn3mde"
            },
            new()
            {
                Name = "Matrix 5",
                Banner = new Uri("https://m.primal.net/KrhZ.jpg"),
                ShortDescription = "Matrix 5 Project",
                Picture = new Uri("https://pfp.nostr.build/5828e07a01a89d6059e85a00ca57680a1b835f2ad197afb2798ad8c7e175cf65.jpg"),
                Id = "angor1qmd8kazm8uzk7s0gddf4amjx4mzj3n5wzgn3mde"
            },
            new()
            {
                Name = "Bitcoin festival",
                Banner = new Uri("https://unchainedcrypto.com/wp-content/uploads/2023/10/bitcoin-hashrate.jpg"),
                Picture = new Uri("https://unchainedcrypto.com/wp-content/uploads/2023/10/bitcoin-hashrate.jpg"),
                Id = "angor1qmd8kazm8uzk7s0gddf4amjx4mzj3n5wzgn3mde"
            },
        ];

        return projects;
    }

    public static string TestNetBitcoinAddress { get; } = "mzHrLAR3WWLE4eCpq82BDCKmLeYRyYXPtm";

    public static SeedWords Seedwords
    {
        get
        {
            List<string> list = ["one", "two", "three", "four", "five", "six", "seven", "eight", "nine", "ten", "eleven", "twelve"];
            return new SeedWords(list.Select((s, i) => new SeedWord(i + 1, s)));
        }
    }
}
