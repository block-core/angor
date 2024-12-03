using System.Collections.Generic;
using System.Linq;
using System.Text;
using NBitcoin;
using NBitcoin.Crypto;
using NBitcoin.DataEncoders;
using NBitcoin.Secp256k1;
using static NBitcoin.Scripting.OutputDescriptor;
using static NBitcoin.Scripting.PubKeyProvider;

namespace Angor.Shared;

public class ProjectIdentifierDerivation 
{
    public static ECPubKey ComputeSharedSecretFounder(ECPrivKey a, ECPubKey B)
    {
        // Let P = B + hash(a�B)�G

        ECPubKey sharedSecret = new ECPubKey(B.GetSharedPubkey(a).Q, null);
        ECPrivKey hashedSharedSecret = ECPrivKey.Create(Hashes.SHA256(sharedSecret.ToBytes()));
        GEJ pmk = hashedSharedSecret.CreatePubKey().Q.ToGroupElementJacobian() + B.Q;
        ECPubKey publicKeyIdentifier = new ECPubKey(pmk.ToGroupElement(), null);

        return publicKeyIdentifier;
    }

    public static ECPrivKey ComputeSharedSecretAngor(ECPubKey A, ECPrivKey b)
    {
        // Let p = b + hash(A�b)�G

        ECPubKey sharedSecret = new ECPubKey(A.GetSharedPubkey(b).Q, null);
        ECPrivKey hashedSharedSecret = ECPrivKey.Create(Hashes.SHA256(sharedSecret.ToBytes()));
        ECPrivKey privateKeyIdentifier  = ECPrivKey.Create((hashedSharedSecret.sec + b.sec).ToBytes());

        return privateKeyIdentifier;
    }

    private static Scalar InputHash(ECPrivKey b, ECPubKey A)
    {
        var hash = Hashes.SHA256(A.TweakMul(b.sec.ToBytes()).ToBytes());
        return new Scalar(hash);
    }

    public static readonly byte[] NUMS =
        Encoders.Hex.DecodeData("50929b74c1a04954b78b4b6035e97a5e078a5a0f28ec96d547bfee9ace803ac0");

    public static ECPubKey ComputeSharedSecretSender(Utxo[] utxos, ECPubKey B)
    {
        using var a = SumPrivateKeys(utxos);
        return ComputeSharedSecret(utxos.Select(x => x.OutPoint).ToArray(), a, B);
    }

    public static ECPubKey ComputeSharedSecretReceiver((OutPoint PrevOut, GE? PubKey)[] inputs, ECPrivKey b)
    {
        var A = SumPublicKeys(inputs.Where(x => x.PubKey is not null).Select(x => (GE)x.PubKey!));
        return ComputeSharedSecret(inputs.Select(x => x.PrevOut).ToArray(), A, b);
    }

    public static ECPubKey ComputeSharedSecretReceiver(ECPubKey tweakData, ECPrivKey b) =>
        new(tweakData.GetSharedPubkey(b).Q, null);

    private static ECPubKey ComputeSharedSecret(OutPoint[] outpoints, ECPrivKey a, ECPubKey B) =>
        DHSharedSecret(InputHash(outpoints, a.CreatePubKey()), B, a);

    private static ECPubKey ComputeSharedSecret(OutPoint[] outpoints, ECPubKey A, ECPrivKey b) =>
        DHSharedSecret(InputHash(outpoints, A), A, b);

    private static ECPubKey DHSharedSecret(Scalar inputHash, ECPubKey pubKey, ECPrivKey privKey) =>
        new(TweakData(inputHash, pubKey).GetSharedPubkey(privKey).Q, null);

    public static ECPubKey TweakData(OutPoint[] inputs, GE?[] As) =>
        TweakData(inputs, SumPublicKeys(As.Where(x => x is not null).Select(x => (GE)x!)));

    public static ECPubKey TweakData(OutPoint[] inputs, ECPubKey A) =>
        TweakData(InputHash(inputs, A), A);

    public static ECPubKey TweakData(Scalar inputHash, ECPubKey pubKey)
    {
        var ret = new ECPubKey((inputHash * pubKey.Q).ToGroupElement(), null);
        return ret;
    }

    // let tk = hash_BIP0352/SharedSecret(serP(ecdh_shared_secret) || ser32(k))
    private static ECPrivKey TweakKey(ECPubKey sharedSecret, uint k) =>
        ECPrivKey.Create(
            TaggedHash(
                "BIP0352/SharedSecret",
                ByteHelpers.Combine(sharedSecret.ToBytes(), Serialize32(k))));

    // let input_hash = hash_BIP0352/Inputs(outpointL || A)
    private static Scalar InputHash(OutPoint[] outpoints, ECPubKey A)
    {
        var outpointL = outpoints.Select(x => x.ToBytes()).Order(BytesComparer.Instance).First();
        var hash = TaggedHash("BIP0352/Inputs", ByteHelpers.Combine(outpointL, A.ToBytes()));
        return new Scalar(hash);
    }

    public static Dictionary<SilentPaymentAddress, SilentPaymentPubKey[]> GetPubKeys(IEnumerable<SilentPaymentAddress> recipients, Utxo[] utxos)
    {
        return recipients
            .GroupBy(x => x.ScanKey, (scanKey, addresses) =>
            {
                var sharedSecret = ComputeSharedSecretSender(utxos, scanKey);
                return addresses.Select((addr, k) => ComputePubKey(addr, (uint)k, sharedSecret));
            })
            .SelectMany(x => x)
            .GroupBy(x => x.Address)
            .ToDictionary(x => x.Key, x => x.ToArray());
    }

    public static IEnumerable<ECXOnlyPubKey> GetPubKeys(SilentPaymentAddress[] addresses, ECPubKey sharedSecret, ECXOnlyPubKey[] outputs)
    {
        var found = 0;
        var n = 0;
        while (found == n)
        {
            var pns = addresses.Select(address => ComputePubKey(address, (uint)n, sharedSecret)).ToArray();
            if (outputs.FirstOrDefault(o => pns.Select(x => x.PubKey.Q).Contains(o.Q)) is { } nonNullOutput)
            {
                yield return nonNullOutput;
                found++;
            }
            else
            {
                foreach (var output in outputs)
                {
                    if (pns.Select(pn => pn.PubKey.Q).Contains(output.Q))
                    {
                        yield return output;
                        found++;
                    }
                }
            }

            n++;
        }
    }

    public static bool IsElegible(Transaction tx)
    {
        var hasTaprootOutputs = tx.Outputs.Any(x => x.ScriptPubKey.IsScriptType(ScriptType.Taproot));
        return hasTaprootOutputs;
    }

    public static Script[] ExtractSilentPaymentScriptPubKeys(SilentPaymentAddress[] addresses, ECPubKey tweakData, Transaction tx, ECPrivKey spendKey)
    {
        if (IsElegible(tx))
        {
            var taprootPubKeys = tx.Outputs
                .Where(x => x.ScriptPubKey.IsScriptType(ScriptType.Taproot))
                .Select(x => PayToTaprootTemplate.Instance.ExtractScriptPubKeyParameters(x.ScriptPubKey))
                .Select(x => ECXOnlyPubKey.Create(x.ToBytes()))
                .ToArray();
            var sharedSecret = ComputeSharedSecretReceiver(tweakData, spendKey);
            var silentPaymentOutputs = GetPubKeys(addresses, sharedSecret, taprootPubKeys);
            return silentPaymentOutputs.Select(x => new TaprootPubKey(x.ToBytes()).ScriptPubKey).ToArray();
        }

        return new Script[0];
    }

    public static ECPubKey CreateLabel(ECPrivKey scanKey, uint label)
    {
        using var m = ECPrivKey.Create(
            TaggedHash(
                "BIP0352/Label",
                ByteHelpers.Combine(scanKey.sec.ToBytes(), Serialize32(label))));
        return m.CreatePubKey();
    }

    private static SilentPaymentPubKey ComputePubKey(SilentPaymentAddress addr, uint k, ECPubKey sharedSecret)
    {
        var pubkey = ComputePubKey(addr.SpendKey, sharedSecret, k).ToXOnlyPubKey();
        return new SilentPaymentPubKey(pubkey, addr);
    }

    public static Script ComputeScriptPubKey(ECPubKey spendKey, ECPubKey sharedSecret, uint k)
    {
        var pubkey = ComputePubKey(spendKey, sharedSecret, k);
        return new TaprootPubKey(pubkey.ToXOnlyPubKey().ToBytes()).ScriptPubKey;
    }

    public static ECPrivKey ComputePrivKey(ECPrivKey spendKey, ECPubKey sharedSecret, uint k)
    {
        using var tk = TweakKey(sharedSecret, k);
        return ECPrivKey.Create((tk.sec + spendKey.sec).ToBytes());
    }

    public static GE? ExtractPubKey(Script? scriptSig, WitScript? txInWitness, Script prevOutScriptPubKey)
    {
        var spk = prevOutScriptPubKey;
        if (txInWitness is { } && spk.IsScriptType(ScriptType.Taproot))
        {
            var pubKeyParameters = PayToTaprootTemplate.Instance.ExtractScriptPubKeyParameters(spk);
            var annex = txInWitness[txInWitness.PushCount - 1][^1] == 0x50 ? 1 : 0;
            if (txInWitness.PushCount > annex &&
                ByteHelpers.CompareFastUnsafe(txInWitness[txInWitness.PushCount - annex - 1][1..33], NUMS))
            {
                return null;
            }
            return ECXOnlyPubKey.Create(pubKeyParameters.ToBytes()).Q;
        }
        if (txInWitness is { } && spk.IsScriptType(ScriptType.P2WPKH))
        {
            var witScriptParameters =
                PayToWitPubKeyHashTemplate.Instance.ExtractWitScriptParameters(txInWitness);
            if (witScriptParameters is { } nonNullWitScriptParameters && nonNullWitScriptParameters.PublicKey.IsCompressed)
            {
                var q = ECPubKey.Create(nonNullWitScriptParameters.PublicKey.ToBytes()).ToXOnlyPubKey().Q;
                return nonNullWitScriptParameters.PublicKey.ToBytes()[0] == 0x02 ? q : q.Negate();
            }
        }
        if (scriptSig is { } && spk.IsScriptType(ScriptType.P2PKH))
        {
            var pk = scriptSig.GetAllPubKeys().First();
            return pk.IsCompressed && pk.GetScriptPubKey(ScriptPubKeyType.Legacy) == spk
                ? ECPubKey.Create(pk.ToBytes()).Q
                : null;
        }
        if (scriptSig is { } && spk.IsScriptType(ScriptType.P2SH))
        {
            var p2sh = PayToScriptHashTemplate.Instance.ExtractScriptSigParameters(scriptSig);
            if (txInWitness is { } && p2sh.RedeemScript.IsScriptType(ScriptType.P2WPKH))
            {
                var witScriptParameters =
                    PayToWitPubKeyHashTemplate.Instance.ExtractWitScriptParameters(txInWitness);
                if (witScriptParameters is { } nonNullWitScriptParameters && nonNullWitScriptParameters.PublicKey.IsCompressed)
                {
                    var q = ECPubKey.Create(nonNullWitScriptParameters.PublicKey.ToBytes()).ToXOnlyPubKey().Q;
                    return nonNullWitScriptParameters.PublicKey.ToBytes()[0] == 0x02 ? q : q.Negate();
                }
            }
        }

        return null;
    }

    private static ECPubKey ComputePubKey(ECPubKey spendKey, ECPubKey sharedSecret, uint k)
    {
        using var tk = TweakKey(sharedSecret, k);

        // Let Pmk = k�G + Bm
        var pmk = tk.CreatePubKey().Q.ToGroupElementJacobian() + spendKey.Q;
        return new ECPubKey(pmk.ToGroupElement(), null);
    }

    private static ECPrivKey SumPrivateKeys(IEnumerable<Utxo> utxos)
    {
        var sum = utxos
            .Select(x => NegateKey(x.SigningKey, x.ScriptPubKey.IsScriptType(ScriptType.Taproot)))
            .Aggregate(Scalar.Zero, (acc, key) => acc.Add(key.sec));

        return ECPrivKey.Create(sum.ToBytes());

        ECPrivKey NegateKey(Key key, bool isTaproot)
        {
            var pk = ECPrivKey.Create(key.ToBytes());
            pk.CreateXOnlyPubKey(out var parity);
            return isTaproot && parity ? ECPrivKey.Create(pk.sec.Negate().ToBytes()) : pk;
        }
    }

    private static ECPubKey SumPublicKeys(IEnumerable<GE> pubKeys) =>
        new(pubKeys.Aggregate(GEJ.Infinity, (acc, key) => acc + key).ToGroupElement(), null);

    private static byte[] TaggedHash(string tag, byte[] data)
    {
        var tagHash = Hashes.SHA256(Encoding.UTF8.GetBytes(tag));
        var concat = ByteHelpers.Combine(tagHash, tagHash, data);
        return Hashes.SHA256(concat);
    }

    private static byte[] Serialize32(uint i)
    {
        var result = new byte[4];
        BitConverter.GetBytes(i).CopyTo(result, 0);
        if (BitConverter.IsLittleEndian)
        {
            Array.Reverse(result);
        }
        return result;
    }



    public record Utxo(OutPoint OutPoint, Key SigningKey, Script ScriptPubKey);

    public record SilentPaymentPubKey(ECXOnlyPubKey PubKey, SilentPaymentAddress Address);
}

public record SilentPaymentAddress(int Version, ECPubKey ScanKey, ECPubKey SpendKey)
{
    //public static SilentPaymentAddress Parse(string encoded, Network network)
    //{
    //    var spEncoder = network.GetSilentPaymentBech32Encoder();
    //    var result = spEncoder.DecodeDataRaw(encoded, out _);
    //    var version = result[0];
    //    if (version != 0)
    //    {
    //        throw new FormatException("Unexpected version of silent payment code");
    //    }

    //    if (result.Length != 107)
    //    {
    //        throw new FormatException("Wrong lenght");
    //    }

    //    var data = spEncoder.FromBase32(result[1..]);
    //    return new SilentPaymentAddress(
    //        Version: 0,
    //        ScanKey: ECPubKey.Create(data[..33]),
    //        SpendKey: ECPubKey.Create(data[33..]));
    //}

    //public string ToWip(Network network)
    //{
    //    var spEncoder = network.GetSilentPaymentBech32Encoder();
    //    var data = new byte[66];
    //    Buffer.BlockCopy(ScanKey.ToBytes(), 0, data, 0, 33);
    //    Buffer.BlockCopy(SpendKey.ToBytes(), 0, data, 33, 33);
    //    var base32 = spEncoder.ToBase32(data);
    //    var buffer = new byte[base32.Length + 1];
    //    buffer[0] = (byte)Version;
    //    Buffer.BlockCopy(base32, 0, buffer, 1, base32.Length);
    //    return spEncoder.EncodeRaw(buffer, Bech32EncodingType.BECH32M);
    //}

    public SilentPaymentAddress DeriveAddressForLabel(ECPubKey mG)
    {
        var bm = (SpendKey.Q.ToGroupElementJacobian() + mG.Q).ToGroupElement();
        return this with { SpendKey = new ECPubKey(bm, null) };
    }
}
