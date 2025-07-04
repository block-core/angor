using System.Text;
using Angor.Shared.Models;
using Blockcore.NBitcoin.DataEncoders;
using Microsoft.Extensions.Logging;
using NBitcoin;
using NBitcoin.Secp256k1;
using Op = Blockcore.Consensus.ScriptInfo.Op;
using OpcodeType = Blockcore.Consensus.ScriptInfo.OpcodeType;
using Script = Blockcore.Consensus.ScriptInfo.Script;
using Sequence = Blockcore.NBitcoin.Sequence;
using uint256 = Blockcore.NBitcoin.uint256;
using Utils = Blockcore.NBitcoin.Utils;

namespace Angor.Shared.Protocol.Scripts;

public class InvestmentScriptBuilder : IInvestmentScriptBuilder
{
    private readonly ILogger<InvestmentScriptBuilder>? _logger;
    private readonly ISeederScriptTreeBuilder _seederScriptTreeBuilder;

    public InvestmentScriptBuilder(ISeederScriptTreeBuilder seederScriptTreeBuilder, ILogger<InvestmentScriptBuilder>? logger = null)
    {
        _seederScriptTreeBuilder = seederScriptTreeBuilder;
        _logger = logger;
    }

    public Script GetInvestorPenaltyTransactionScript(string investorKey, int punishmentLockDays)
    {
        if (punishmentLockDays > 388)
        {
            // the actual number is 65535*512 seconds (388 days) 
            // https://en.bitcoin.it/wiki/Timelock
            throw new ArgumentOutOfRangeException(nameof(punishmentLockDays), $"Invalid CSV value {punishmentLockDays}");
        }
        
        var sequence = new Sequence(TimeSpan.FromDays(punishmentLockDays));

        return new(new List<Op>
        {
            Op.GetPushOp(new NBitcoin.PubKey(investorKey).ToBytes()),
            OpcodeType.OP_CHECKSIGVERIFY,
            Op.GetPushOp((uint)sequence),
            OpcodeType.OP_CHECKSEQUENCEVERIFY
        });
    }

    class SHA256 : IDisposable
	{
		public void Initialize()
		{
			sha.Initialize();
			_Pos = 0;
		}
		/// <summary>
		/// Initializes a sha256 struct and writes the 64 byte string
		/// SHA256(tag)||SHA256(tag) into it.
		/// </summary>
		/// <param name="tag"></param>
		public void InitializeTagged(ReadOnlySpan<byte> tag)
		{
			Span<byte> buf = stackalloc byte[32];
			Initialize();
			Write(tag);
			GetHash(buf);
			Initialize();
			Write(buf);
			Write(buf);
		}
		/// <summary>
		/// Initializes a sha256 struct and writes the 64 byte string
		/// SHA256(tag)||SHA256(tag) into it.
		/// </summary>
		/// <param name="tag"></param>
		public void InitializeTagged(string tag)
		{
			InitializeTagged(Encoding.ASCII.GetBytes(tag));
		}
		System.Security.Cryptography.SHA256 sha = System.Security.Cryptography.SHA256.Create();
		int _Pos;
		byte[] _Buffer = System.Buffers.ArrayPool<byte>.Shared.Rent(64);
		public void Write(ReadOnlySpan<byte> buffer)
		{
			int copied = 0;
			var innerSpan = new Span<byte>(_Buffer, _Pos, _Buffer.Length - _Pos);
			while (!buffer.IsEmpty)
			{
				int toCopy = Math.Min(innerSpan.Length, buffer.Length);
				buffer.Slice(0, toCopy).CopyTo(innerSpan.Slice(0, toCopy));
				buffer = buffer.Slice(toCopy);
				innerSpan = innerSpan.Slice(toCopy);
				copied += toCopy;
				_Pos += toCopy;
				if (ProcessBlockIfNeeded())
					innerSpan = _Buffer.AsSpan();
			}
		}
		public void Write(byte b)
		{
			_Buffer[_Pos] = b;
			_Pos++;
			ProcessBlockIfNeeded();
		}
		private bool ProcessBlockIfNeeded()
		{
			if (_Pos == _Buffer.Length)
			{
				ProcessBlock();
				return true;
			}
			return false;
		}
		private void ProcessBlock()
		{
			sha.TransformBlock(_Buffer, 0, _Pos, null, -1);
			_Pos = 0;
		}

		public byte[] GetHash()
		{
			var r = new byte[32];
			GetHash(r);
			return r;
		}
		public void GetHash(Span<byte> output)
		{
			ProcessBlock();
			sha.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
			var hash1 = sha.Hash;
			hash1.AsSpan().CopyTo(output);
		}

		public void Dispose()
		{
			System.Buffers.ArrayPool<byte>.Shared.Return(_Buffer, true);
			sha.Dispose();
		}
	}
    
    public ProjectScripts BuildProjectScriptsForStage(ProjectInfo projectInfo, string investorKey, int stageIndex,
        uint256? hashOfSecret)
    {
	    try
	    {
		    var pubKey = new NBitcoin.PubKey(projectInfo.FounderRecoveryKey);

		    _logger?.LogInformation($"pubKey {pubKey.ToHex()}");
		    _logger?.LogInformation($"pubKey to bytes test {Encoders.Hex.EncodeData(pubKey.ToBytes())}");
		    _logger?.LogInformation($"pubKey TaprootInternalKey{pubKey.TaprootInternalKey.ToString()}");


		    var feCreate = NBitcoin.Secp256k1.FE.TryCreate(new Span<byte>(pubKey.ToBytes()), out var x);
		    
		    _logger?.LogInformation($"FE create { feCreate } FE {x.ToString()} FE bytes {Encoders.Hex.EncodeData(x.ToBytes())}");

		    if (GE.TryCreateXQuad(x, out var geQuad))
			    _logger?.LogInformation($"TryCreateXQuad {geQuad.ToString()}");

		    if (GE.TryCreateXOVariable(x, false, out var ge))
		    {
			    _logger?.LogInformation($"GE {ge.ToString()}");
		    }

		    TaprootInternalPubKey.TryCreate(pubKey.ToBytes()[1..], out var taprootInternalPubKey);
		    
		    _logger?.LogInformation($"taprootInternalPubKey {Encoders.Hex.EncodeData(taprootInternalPubKey.ToBytes())}");
		    
		    var tweak = taprootInternalPubKey.ComputeTapTweak(null);

		    if (ECXOnlyPubKey.TryCreate(pubKey.ToBytes()[1..], out var ecxOnlyPubKey))
		    {
			    _logger?.LogInformation($"ECXOnlyPubKey x {Encoders.Hex.EncodeData(ecxOnlyPubKey.Q.x.ToBytes())}");
			    _logger?.LogInformation($"ECXOnlyPubKey y {Encoders.Hex.EncodeData(ecxOnlyPubKey.Q.y.ToBytes())}");
			    _logger?.LogInformation($"ECXOnlyPubKey ascii {Encoding.ASCII.GetString(ecxOnlyPubKey.ToBytes())}");
			    _logger?.LogInformation($"ECXOnlyPubKey hex {Encoders.Hex.EncodeData(ecxOnlyPubKey.ToBytes())}");
		    }

		    var ec = ecxOnlyPubKey?.AddTweak(tweak);
		    
		    _logger?.LogInformation($"ec {Encoders.Hex.EncodeData(ec.ToBytes())}");
		    
		    var key = ecxOnlyPubKey?.AddTweak(tweak).ToXOnlyPubKey(out var taprootPubKeyTweak);
		    
		    _logger?.LogInformation($"ECXOnlyPubKey key only ---- {Encoders.Hex.EncodeData(key?.ToBytes())}");
		    
		    _logger?.LogInformation($"tweak {Encoders.Hex.EncodeData(tweak)}");
		    
		    var taprootFullPubKeyTest = TaprootFullPubKey.Create(taprootInternalPubKey,null);
		    
		    
		    _logger?.LogInformation($"CheckTapTweak {taprootFullPubKeyTest.CheckTapTweak(taprootInternalPubKey)}");
		    ;
		    
		    _logger?.LogInformation( $"taprootFullPubKeyTest is not null {taprootFullPubKeyTest.ToString()} {Encoders.Hex.EncodeData(taprootFullPubKeyTest.ToBytes())}");
		    _logger?.LogInformation( $"taprootFullPubKeyTest InternalKey {taprootFullPubKeyTest.InternalKey} ");
		    _logger?.LogInformation( $"taprootFullPubKeyTest OutputKey {taprootFullPubKeyTest.OutputKey} ");
		    _logger?.LogInformation( $"taprootFullPubKeyTest Tweak {taprootFullPubKeyTest.Tweak} ");
		    _logger?.LogInformation( $"taprootFullPubKeyTest MerkleRoot {taprootFullPubKeyTest.MerkleRoot} ");


		    _logger?.LogInformation( $"taprootInternalPubKey.GetTaprootFullPubKey {Encoders.Hex.EncodeData(taprootInternalPubKey.GetTaprootFullPubKey().ToBytes())} ");
		
		    TaprootPubKey.TryCreate(pubKey.ToBytes(), out var taprootPubKey);

		    _logger?.LogInformation($"taprootPubKey is null {taprootPubKey == null}");
		    
		    _logger?.LogInformation($"taprootPubKey {taprootPubKey.ToString()}");
		    _logger?.LogInformation($"taprootPubKey {taprootPubKey.ToBytes()}");
		    _logger?.LogInformation($"taprootPubKey {taprootPubKey.ScriptPubKey.ToHex()}");
		    
		    

		    var toSpan = new Span<byte>(new byte[32]);


		    _logger?.LogInformation(
			    $"pubKey TaprootInternalKey.GetTaprootFullPubKey{pubKey.TaprootInternalKey.GetTaprootFullPubKey(null)}");
		    _logger?.LogInformation(
			    $"pubKey TaprootInternalKey.AsTaprootPubKey{pubKey.TaprootInternalKey.AsTaprootPubKey().ToBytes()}");
		    _logger?.LogInformation(
			    $"pubKey TaprootInternalKey.AsTaprootPubKey{pubKey.TaprootInternalKey.AsTaprootPubKey().ToString()}");

		    var taprootFullPubKey = pubKey.GetTaprootFullPubKey();


		    var SHA256 = new SHA256();
		    SHA256.InitializeTagged("TapTweak");

		    var span = new Span<byte>(new byte[32]);
		    taprootFullPubKey.InternalKey.ComputeTapTweak(null, span);

		    _logger?.LogInformation($"sha256 {Encoders.Hex.EncodeData(SHA256.GetHash())}");
		    SHA256.Write(span);
		    _logger?.LogInformation($"sha256 {Encoders.Hex.EncodeData(SHA256.GetHash())}");
		    _logger?.LogInformation($"span {Encoders.Hex.EncodeData(span.ToArray())}");

		    SHA256.GetHash(span);
		    _logger?.LogInformation($"span {Encoders.Hex.EncodeData(span.ToArray())}");

		    _logger?.LogInformation($"founderFullPubKey {new NBitcoin.PubKey(projectInfo.FounderRecoveryKey).ToHex()}");


		    if (taprootFullPubKey.OutputKey.ToBytes().Equals(new byte[32]))
		    {
			    throw new Exception("Invalid founder recovery key, it must be a valid taproot key");
		    }

		    _logger?.LogInformation($"founderFullPubKey taproot {taprootFullPubKey}");

		    _logger?.LogInformation($"OutputKey: {Encoders.Hex.EncodeData(taprootFullPubKey.OutputKey.ToBytes())}");
		    _logger?.LogInformation($"OutputKeyParity: {taprootFullPubKey.OutputKeyParity}");
		    _logger?.LogInformation($"InternalKey: {Encoders.Hex.EncodeData(taprootFullPubKey.InternalKey.ToBytes())}");
		    _logger?.LogInformation($"Tweak: {Encoders.Hex.EncodeData(taprootFullPubKey.Tweak.ToArray())}");
		    _logger?.LogInformation($"MerkleRoot: {taprootFullPubKey.MerkleRoot}");

		    _logger?.LogInformation($"Environment: {Environment.MachineName}, {Environment.Version}");

		    _logger?.LogInformation(
			    $"ScriptPubKey : {Encoders.Hex.EncodeData(taprootFullPubKey.ScriptPubKey.ToBytes())}");
		    _logger?.LogInformation($"ScriptPubKey: {taprootFullPubKey.ScriptPubKey.ToHex()}");
		    _logger?.LogInformation(
			    $"ScriptPubKey: {Encoders.Hex.EncodeData(taprootFullPubKey.ScriptPubKey.ToCompressedBytes())}");
		    _logger?.LogInformation(
			    $"ScriptPubKey: {Encoders.Hex.EncodeData(taprootFullPubKey.ScriptPubKey.ToTapScript(TapLeafVersion.C0).Script.ToBytes())}");
	    }
	    catch (Exception e)
	    {
		    _logger?.LogError(e, "Error building project scripts for stage");
		    throw;
	    }

	    // regular investor pre-co-sign with founder to gets funds with penalty
        var recoveryOps = new List<Op>
        {
            Op.GetPushOp(new NBitcoin.PubKey(projectInfo.FounderRecoveryKey).GetTaprootFullPubKey().ToBytes()),
            OpcodeType.OP_CHECKSIGVERIFY,
            Op.GetPushOp(new NBitcoin.PubKey(investorKey).GetTaprootFullPubKey().ToBytes()),
        };

        _logger?.LogInformation(
            $"recovery ops:{Encoders.Hex.EncodeData(recoveryOps.SelectMany(op => op.ToBytes()).ToArray())}");
        
        
        var secretHashOps = hashOfSecret == null
            ? new List<Op> { OpcodeType.OP_CHECKSIG }
            : new List<Op>
            {
                OpcodeType.OP_CHECKSIGVERIFY,
                OpcodeType.OP_HASH256,
                Op.GetPushOp(new uint256(hashOfSecret).ToBytes()),
                OpcodeType.OP_EQUAL
            };
        
        recoveryOps.AddRange(secretHashOps);

        _logger?.LogInformation(
            $"recovery ops + hashes:{Encoders.Hex.EncodeData(recoveryOps.SelectMany(op => op.ToBytes()).ToArray())}");
        
        var seeders = hashOfSecret == null && projectInfo.ProjectSeeders.SecretHashes.Any()
            ? _seederScriptTreeBuilder.BuildSeederScriptTree(investorKey,
                projectInfo.ProjectSeeders.Threshold,
                projectInfo.ProjectSeeders.SecretHashes).ToList()
            : new List<Script>();
        
        var result = new ProjectScripts()
        {
            Founder = GetFounderSpendScript(projectInfo.FounderKey, projectInfo.Stages[stageIndex].ReleaseDate),
            Recover = new Script(recoveryOps),
            EndOfProject = GetEndOfProjectInvestorSpendScript(investorKey, projectInfo.ExpiryDate),
            Seeders = seeders
        };
        return result;
    }

    private static Script GetFounderSpendScript(string founderKey, DateTime stageReleaseDate)
    {
        long locktimeFounder = Utils.DateTimeToUnixTime(stageReleaseDate);   
        
        // founder gets funds after stage started
        return new Script(new List<Op>
        {
            Op.GetPushOp(new NBitcoin.PubKey(founderKey).GetTaprootFullPubKey().ToBytes()),
            OpcodeType.OP_CHECKSIGVERIFY,
            Op.GetPushOp(locktimeFounder),
            OpcodeType.OP_CHECKLOCKTIMEVERIFY
        });
    }

    private static Script GetEndOfProjectInvestorSpendScript(string investorKey, DateTime projectExpieryDate)
    {
        long locktimeExpiery = Utils.DateTimeToUnixTime(projectExpieryDate);
        
        // project ended and investor can collect remaining funds
        return new Script(new List<Op>
        {
            Op.GetPushOp(new NBitcoin.PubKey(investorKey).GetTaprootFullPubKey().ToBytes()),
            OpcodeType.OP_CHECKSIGVERIFY,
            Op.GetPushOp(locktimeExpiery),
            OpcodeType.OP_CHECKLOCKTIMEVERIFY
        });
    }
}