using Angor.Shared;
using Angor.Shared.Protocol.Scripts;
using Blockcore.NBitcoin;
using Moq;

namespace Angor.Test.Protocol.Scripts;

public class ProjectScriptsBuilderTest
{
    private ProjectScriptsBuilder _sut;

    private Mock<IDerivationOperations> _derivationOperations;
    
    public ProjectScriptsBuilderTest()
    {
        _derivationOperations = new Mock<IDerivationOperations>();

        _sut = new ProjectScriptsBuilder(_derivationOperations.Object);
    }


    [Fact]
    public void GetAngorFee_CallsDerivationOperations()
    {
        var expectedKey = Guid.NewGuid().ToString();

        _sut.GetAngorFeeOutputScript(expectedKey);
        
        _derivationOperations.Verify(_ => _.AngorKeyToScript(expectedKey),Times.Once);
    }
    
    [Fact]
    public void GetAngorFee_ReturnsFromDerivationOperations()
    {
        var key = Guid.NewGuid().ToString();
        var expectedResult = new Key().ScriptPubKey;


        _derivationOperations.Setup(_ => _.AngorKeyToScript(key))
            .Returns(expectedResult);
        
        var result = _sut.GetAngorFeeOutputScript(key);
        
        Assert.Same(expectedResult,result);
    }

    [Fact]
    public void BuildInvestorInfoScript()
    {
        //TODO
    }
    
    [Fact]
    public void BuildSeederInfoScript()
    {
        //TODO
    }
    
    [Fact]
    public void GetInvestmentDataFromOpReturnScript()
    {
        //TODO
    }
}