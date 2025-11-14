using Angor.Contexts.CrossCutting;
using Angor.Contexts.Funding.Shared;
using Angor.Contexts.Funding.Tests.TestDoubles;
using Angor.Shared;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using Xunit.Abstractions;

namespace Angor.Contexts.Funding.Tests;

public class NostrDecrypterTests(ITestOutputHelper output)
{
    private static readonly WalletId TestWalletId = new("test-wallet-nostr-decrypter");

    [Fact(Skip = "Skipping failing test: missing logger registration for derivation operations.")]
    public async Task Decrypt_Nostr_message()
    {
        // Arrange
        var sut = CreateSut();
        var nostrMessage = new DirectMessage("SOME ID", "c57cce49441740454e54fee15131b9699d54d4928a5eaa0756214dc21961cf1b", "gYpNKJuzweBOfsOXOOInoyEXI+jlOxs2dQ//Yk3CSpkvUQPs6od9eq/HCUGBPetHlTBlmlJheVNYzl+u+4NcShzgV2kmOb7rbH88UUCZXRyXdxwwnGU8rvM4BKNc/WqS3Rsyny+hDpy/YpEpsSMH46kTAqPm6+PjyyIoTGNMqAui95RdTNMHXju6hLXse6kTL93aD8xpCsLVZ773PaJxtO8qzVmCJ9Ton+TMkD3ujxcUAv/Dy03vRQWp96iNSfsBif6OUlenGPk5YUzgKscjNVVVxOgLCB7hdlM9NQiTDB3EKabl6p7L9Dg77UftRcrHwYE44QYtgOjTggNRtoWYhAxgITp4IHtIOLJRIBexlxrY+2R8Zfkd1w0qvTm41zi9eITB0/rJEE/u8jMFJLVxDMLcaGVWuj1/9NlcZ9jTpf5ANOr6VnWDi2JRUsfJXDxUNyYSQttoclZVYD1gLjv/+ags+aBQzpiz5XOAC1NVOouKTE4Wj4b9MLvMJLMfyjtmBPBc++7uy0dOX6YVQqorOzNtk6NkOQFPDdCZK5zmQB2twWwwo6TlzKUT3n3msoeHi7AuBLAxoD7v2hmY61SMt7Z8tM4kOmaxMF+ed1ZKrD1gyfWniOD/4GXj+lovTPw4vOZRLmfYTTCKJROB/qBheH5tyuNahn4FiIWYxATTBO3jJoKw9evQdf8haDptwlPTZUwlf3C+T0nf+9Z+bXSWaRPsORQzNgWYnRys/G4OpodXiVFwdH+6rYeXDMXAiCMEDXBBfT2e5QZxVuJuSbhTW75jNvDEaHrg12UxvA+7nqCy4rpHC3nDQaABEprXdYK6bANqIWSsNCsmUhSjLfpH+gljJ5iiH+yE6kf/tGGNnSS4pKa9cPSMbSB68eIWwUeaNRRBtcJkvr42K35jUE2vEQhqs3ojQlMItgy4/YvcXP6+DMzGZbxdDWO8u0xsE0qNpkCavwi/+UgVsYj5dEK32nt8kiBryr6/UJyWprOa8lUQ=?iv=+uu8GD5eAnBhjP3FjCVeSQ==", DateTime.Now);
        
        // Act
        var clientKeyResult = await sut.Decrypt(TestWalletId, new ProjectId("angor1qatlv9htzte8vtddgyxpgt78ruyzaj57n4l7k46"), nostrMessage);
        
        // Assert
        Assert.True(clientKeyResult.IsSuccess);
        Assert.NotEmpty(clientKeyResult.Value);
    }
    
    
    private NostrDecrypter CreateSut()
    {
        var serviceCollection = new ServiceCollection();

        var logger = new LoggerConfiguration().WriteTo.TestOutput(output).CreateLogger();
        FundingContextServices.Register(serviceCollection, logger);
        serviceCollection.AddSingleton<ISeedwordsProvider>(sp => new TestingSeedwordsProvider("oven suggest panda hip orange cheap kite focus cross never tornado forget", "", sp.GetRequiredService<IDerivationOperations>()));

        var serviceProvider = serviceCollection.BuildServiceProvider();

        return ActivatorUtilities.CreateInstance<NostrDecrypter>(serviceProvider);
    }
}