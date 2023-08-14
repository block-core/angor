using Angor.Shared;
using Angor.Shared.Networks;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Angor.Test.ProtocolNew;

public class AngorTestData
{
    internal string angorRootKey =
        "tpubD8JfN1evVWPoJmLgVg6Usq2HEW9tLqm6CyECAADnH5tyQosrL6NuhpL9X1cQCbSmndVrgLSGGdbRqLfUbE6cRqUbrHtDJgSyQEY2Uu7WwTL";
    
    internal Mock<INetworkConfiguration> _networkConfiguration;
    internal DerivationOperations _derivationOperations; 

    public AngorTestData()
    {
        _networkConfiguration = new Mock<INetworkConfiguration>();
        _networkConfiguration.Setup(_ => _.GetNetwork())
            .Returns(Networks.Bitcoin.Testnet());
        
        _derivationOperations = new DerivationOperations(new HdOperations(),
            new NullLogger<DerivationOperations>(), _networkConfiguration.Object);
    }
}