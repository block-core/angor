using Angor.Shared;
using Angor.Shared.Models;
using Angor.Shared.Networks;
using Blockcore.Networks;

namespace Angor.Client;

public class NetworkConfiguration : INetworkConfiguration
{
    public static string AngorTestKey = "tpubD8JfN1evVWPoJmLgVg6Usq2HEW9tLqm6CyECAADnH5tyQosrL6NuhpL9X1cQCbSmndVrgLSGGdbRqLfUbE6cRqUbrHtDJgSyQEY2Uu7WwTL";

    public Network GetNetwork()
    {
        return new BitcoinSignet();
    }

    public SettingsUrl GetIndexerUrl()
    {
        // return new IndexerUrl{Symbol = "", Url = "http://10.22.156.65:9910/api"};
        //return new IndexerUrl { Symbol = "", Url = "http://207.180.254.78:9910/api" };
        return new SettingsUrl { Name = "", Url = "https://tbtc.indexer.angor.io/api" };
    }

    public SettingsUrl GetExplorerUrl()
    {
        //return new IndexerUrl { Symbol = "", Url = "http://10.22.156.65:9911/btc/explorer" };
        //return new IndexerUrl { Symbol = "", Url = "http://207.180.254.78:9911/btc/explorer" };
        return new SettingsUrl { Name = "", Url = "https://explorer.angor.io/btc/explorer" };
    }

    public static List<ProjectInfo> CreateFakeProjects()
    {
        return new List<ProjectInfo>
            {
                new ProjectInfo
                {
                    StartDate = DateTime.UtcNow,
                    PenaltyDate = DateTime.UtcNow,
                    ExpiryDate = DateTime.UtcNow,
                    TargetAmount = 300,
                    ProjectIdentifier = "angor" + Guid.NewGuid().ToString("N"),
                    Stages = new List<Stage>
                    {
                        new Stage { AmountToRelease = 10, ReleaseDate = DateTime.UtcNow.AddDays(1) },
                        new Stage { AmountToRelease = 30, ReleaseDate = DateTime.UtcNow.AddDays(2) },
                        new Stage { AmountToRelease = 60, ReleaseDate = DateTime.UtcNow.AddDays(3) },
                    }
                },
                new ProjectInfo
                {
                    StartDate = DateTime.UtcNow,
                    PenaltyDate = DateTime.UtcNow,
                    ExpiryDate = DateTime.UtcNow,
                    TargetAmount = 200,
                    ProjectIdentifier = "angor" + Guid.NewGuid().ToString("N"),
                    Stages = new List<Stage>
                    {
                        new Stage { AmountToRelease = 10, ReleaseDate = DateTime.UtcNow.AddDays(1) },
                        new Stage { AmountToRelease = 30, ReleaseDate = DateTime.UtcNow.AddDays(2) },
                        new Stage { AmountToRelease = 60, ReleaseDate = DateTime.UtcNow.AddDays(3) },
                    }
                },
                new ProjectInfo
                {
                    StartDate = DateTime.UtcNow,
                    PenaltyDate = DateTime.UtcNow,
                    ExpiryDate = DateTime.UtcNow,
                    TargetAmount = 100,
                    ProjectIdentifier = "angor" + Guid.NewGuid().ToString("N"),
                    Stages = new List<Stage>
                    {
                        new Stage { AmountToRelease = 10, ReleaseDate = DateTime.UtcNow.AddDays(1) },
                        new Stage { AmountToRelease = 30, ReleaseDate = DateTime.UtcNow.AddDays(2) },
                        new Stage { AmountToRelease = 60, ReleaseDate = DateTime.UtcNow.AddDays(3) },
                    }
                },
            };
    }
}