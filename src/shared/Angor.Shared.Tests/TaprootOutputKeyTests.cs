using Angor.Shared.Models;
using Angor.Shared.Networks;
using Angor.Shared.Protocol.Scripts;
using Angor.Test.DataBuilders;
using Blockcore.NBitcoin;
using Blockcore.NBitcoin.Crypto;

namespace Angor.Test;

/// <summary>
/// Validates that TaprootScriptBuilder.CreateStage produces non-zero taproot output keys.
/// Regression test for Android bug where all stage outputs were 0x0000...0000.
/// </summary>
public class TaprootOutputKeyTests
{
    private readonly TaprootScriptBuilder _sut = new();

    [Fact]
    public void CreateStage_WithValidScripts_ProducesNonZeroTaprootKey()
    {
        // Arrange — same pattern as ScriptTest.BuildStageScriptsSeeder
        var funderKey = new Key();
        var funderRecoveryKey = new Key();
        var investorKey = new Key();
        var secret = new Key();
        var seeders = new ProjectSeeders();

        var scripts = ScriptBuilder.BuildScripts(
            funderKey.PubKey.ToHex(),
            funderRecoveryKey.PubKey.ToHex(),
            investorKey.PubKey.ToHex(),
            Hashes.Hash256(secret.ToBytes()).ToString(),
            DateTime.UtcNow,
            DateTime.UtcNow.AddDays(365),
            seeders);

        // Act
        var stageScript = _sut.CreateStage(Networks.Bitcoin.Testnet(), scripts);

        // Assert — the script should be OP_1 <32 bytes> (34 bytes total for P2TR)
        var scriptBytes = stageScript.ToBytes();
        Assert.Equal(34, scriptBytes.Length);
        Assert.Equal(0x51, scriptBytes[0]); // OP_1 (taproot version)
        Assert.Equal(0x20, scriptBytes[1]); // PUSH 32 bytes

        // The 32-byte taproot output key must NOT be all zeros
        var outputKeyBytes = scriptBytes.AsSpan(2, 32);
        Assert.False(outputKeyBytes.SequenceEqual(new byte[32]),
            "Taproot output key is all zeros — taproot tree computation failed");
    }

    [Fact]
    public void CreateStage_WithoutSecret_ProducesNonZeroTaprootKey()
    {
        // Arrange — no secret hash, no seeders (like the failing Android transaction)
        var funderKey = new Key();
        var funderRecoveryKey = new Key();
        var investorKey = new Key();
        var seeders = new ProjectSeeders();

        var scripts = ScriptBuilder.BuildScripts(
            funderKey.PubKey.ToHex(),
            funderRecoveryKey.PubKey.ToHex(),
            investorKey.PubKey.ToHex(),
            null,
            DateTime.UtcNow,
            DateTime.UtcNow.AddDays(365),
            seeders);

        // Act
        var stageScript = _sut.CreateStage(Networks.Bitcoin.Testnet(), scripts);

        // Assert
        var scriptBytes = stageScript.ToBytes();
        Assert.Equal(34, scriptBytes.Length);
        Assert.Equal(0x51, scriptBytes[0]);
        Assert.Equal(0x20, scriptBytes[1]);

        var outputKeyBytes = scriptBytes.AsSpan(2, 32);
        Assert.False(outputKeyBytes.SequenceEqual(new byte[32]),
            "Taproot output key is all zeros — taproot tree computation failed");
    }

    [Fact]
    public void CreateStage_DifferentStages_ProduceDifferentTaprootKeys()
    {
        // Arrange — two stages with different timelocks should produce different taproot keys
        var funderKey = new Key();
        var funderRecoveryKey = new Key();
        var investorKey = new Key();
        var seeders = new ProjectSeeders();

        var scripts1 = ScriptBuilder.BuildScripts(
            funderKey.PubKey.ToHex(),
            funderRecoveryKey.PubKey.ToHex(),
            investorKey.PubKey.ToHex(),
            null,
            DateTime.UtcNow.AddDays(30),
            DateTime.UtcNow.AddDays(365),
            seeders);

        var scripts2 = ScriptBuilder.BuildScripts(
            funderKey.PubKey.ToHex(),
            funderRecoveryKey.PubKey.ToHex(),
            investorKey.PubKey.ToHex(),
            null,
            DateTime.UtcNow.AddDays(60),
            DateTime.UtcNow.AddDays(365),
            seeders);

        // Act
        var stage1 = _sut.CreateStage(Networks.Bitcoin.Testnet(), scripts1);
        var stage2 = _sut.CreateStage(Networks.Bitcoin.Testnet(), scripts2);

        // Assert — different timelocks → different taproot keys
        Assert.False(stage1.ToBytes().SequenceEqual(stage2.ToBytes()),
            "Two stages with different timelocks produced identical taproot keys");
    }

    [Fact]
    public void CreateUnspendableInternalKey_ReturnsConsistentNonZeroKey()
    {
        // Act
        var key1 = TaprootScriptBuilder.CreateUnspendableInternalKey();
        var key2 = TaprootScriptBuilder.CreateUnspendableInternalKey();

        // Assert — deterministic
        Assert.Equal(key1.ToString(), key2.ToString());

        // Assert — not all zeros
        var keyBytes = key1.ToBytes();
        Assert.False(keyBytes.All(b => b == 0),
            "Unspendable internal key is all zeros");
    }
}
