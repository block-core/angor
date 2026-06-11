# Taproot ARM64 JIT Bug: `GetTaprootFullPubKey` Returns All-Zero Keys on .NET 10 Android

## Summary

`PubKey.GetTaprootFullPubKey()` and `TaprootFullPubKey.Create()` from NBitcoin produce
all-zero (32 bytes of `0x00`) output keys when running on **.NET 10 ARM64 Android**.
This causes Bitcoin transactions to contain unspendable P2TR outputs with invalid
script pubkeys.

## Impact

- All taproot script building (`InvestmentScriptBuilder`, `SeederScriptTreeBuilder`) embeds
  all-zero pubkeys into `OP_CHECKSIG` / `OP_CHECKSIGVERIFY` scripts
- `TaprootSpendInfo.WithHuffmanTree` produces an all-zero output key, generating invalid
  P2TR addresses
- Signature verification via `TaprootFullPubKey.VerifySignature` always fails
- Transactions broadcast with these scripts lock funds permanently — the all-zero key has
  no known private key

### Affected transaction

`160f198ba0f7dacbc097d6a61703b3121af38b7362d17b063cd9152f74012598` — broadcast on
signet with all-zero taproot output keys.

## Root Cause

The bug is in `TaprootFullPubKey.ComputeTapTweak` (NBitcoin v7.0.46, net6.0 TFM assembly).
This method **reuses the same `Span<byte>` buffer** as both scratch space and output:

```csharp
// NBitcoin/BIP341/TaprootFullPubKey.cs — current code (buggy on .NET 10 ARM64)
internal static void ComputeTapTweak(
    TaprootInternalPubKey internalKey, uint256? merkleRoot, Span<byte> tweak32)
{
    using SHA256 sha = new SHA256();
    sha.InitializeTagged("TapTweak");
    internalKey.pubkey.WriteToSpan(tweak32);  // ← writes pubkey INTO output span
    sha.Write(tweak32);                        // ← hashes from same span
    if (merkleRoot is uint256)
    {
        merkleRoot.ToBytes(tweak32);           // ← writes merkle root INTO output span
        sha.Write(tweak32);
    }
    sha.GetHash(tweak32);                      // ← writes hash result BACK to same span
}
```

The caller in `TaprootFullPubKey.Create` passes a `byte[]` that is implicitly converted
to `Span<byte>` for `ComputeTapTweak`, then the same `byte[]` is implicitly converted to
`ReadOnlySpan<byte>` for `AddTweak`:

```csharp
public static TaprootFullPubKey Create(TaprootInternalPubKey internalKey, uint256? merkleRoot)
{
    byte[] array = new byte[32];
    ComputeTapTweak(internalKey, merkleRoot, array);      // byte[] → Span<byte>
    var outputKey = internalKey.pubkey.AddTweak(array);    // byte[] → ReadOnlySpan<byte>
    // ...
}
```

On the **.NET 10 ARM64 JIT**, this dual implicit conversion from the same `byte[]` within
a single method — combined with the span aliasing inside `ComputeTapTweak` — triggers a
JIT codegen bug. The result of `AddTweak` returns an `ECPubKey` with an all-zero `Q` point.

### Key evidence

The **exact same operations work correctly** when called from a different assembly:

| Operation | Called from NBitcoin.dll | Called from Angor.Shared.dll |
|-----------|------------------------|------------------------------|
| `ComputeTapTweak` | Produces correct tweak `3CF5216D...` | Produces correct tweak `3CF5216D...` |
| `AddTweak(tweak)` | Returns all-zero Q | Returns correct `DA4710...` |
| `TryAddTweak(tweak)` | N/A (not called) | Returns correct `DA4710...` |
| `AddTweak(1)` | N/A | Returns correct `C6047F...` (2*G) |

The tweak stored in the resulting `TaprootFullPubKey.Tweak` property is **correct** — only
the output key (`base.pubkey.Q`) is zero. This proves the JIT is miscompiling the specific
call chain inside `TaprootFullPubKey.Create`.

### Not a tiered compilation issue

Setting `System.Runtime.TieredCompilation=false` via `RuntimeHostConfigurationOption` does
not fix the bug. It is a fundamental JIT codegen issue on ARM64.

### Not an NBitcoin version issue

The `ComputeTapTweak` and `Create` code is identical in NBitcoin v7.0.46 and v10.0.3
(verified via ILSpy decompilation). Upgrading NBitcoin.Secp256k1 from 3.1.1 to 3.2.0 also
does not fix it.

## Environment

- Runtime: .NET 10.0.7
- OS: Android (API level 33)
- Architecture: ARM64
- NBitcoin: 7.0.46 (net6.0 TFM)
- NBitcoin.Secp256k1: 3.2.0 (net6.0 TFM)
- Device: Samsung Galaxy (WiFi ADB at 192.168.1.113:5555)

## Fix Applied (NBitcoin upstream)

The fix in `ComputeTapTweak` uses a **separate `stackalloc` buffer** for intermediate
serialization instead of reusing the output span:

```csharp
// Fixed version
internal static void ComputeTapTweak(
    TaprootInternalPubKey internalKey, uint256? merkleRoot, Span<byte> tweak32)
{
    Span<byte> buf = stackalloc byte[32];          // ← separate scratch buffer
    using SHA256 sha = new SHA256();
    sha.InitializeTagged("TapTweak");
    internalKey.pubkey.WriteToSpan(buf);            // ← write to scratch, not output
    sha.Write(buf);
    if (merkleRoot is uint256)
    {
        merkleRoot.ToBytes(buf);                    // ← write to scratch, not output
        sha.Write(buf);
    }
    sha.GetHash(tweak32);                           // ← only final hash goes to output
}
```

This eliminates the span aliasing pattern that triggers the ARM64 JIT bug.

## Workaround Applied (Angor)

Since we cannot wait for NBitcoin to release a fix, `TaprootKeyHelper` in
`Angor.Shared.Protocol.Scripts` replicates the BIP341 tap tweak computation entirely
within Angor's assembly. All call sites that previously used
`new PubKey(key).GetTaprootFullPubKey().ToBytes()` now use
`TaprootKeyHelper.GetTaprootOutputKeyBytes(key)`.

The workaround works because the .NET 10 ARM64 JIT compiles the same sequence of
operations correctly when the call site is in a different assembly than `TaprootFullPubKey.Create`.

### Files changed

- `src/shared/Angor.Shared/Protocol/Scripts/TaprootKeyHelper.cs` — new workaround helper
- `src/shared/Angor.Shared/Protocol/Scripts/InvestmentScriptBuilder.cs` — uses `TaprootKeyHelper`
- `src/shared/Angor.Shared/Protocol/Scripts/SeederScriptTreeBuilder.cs` — uses `TaprootKeyHelper`
- `src/shared/Angor.Shared/Protocol/InvestorTransactionActions.cs` — uses `TaprootKeyHelper` for sig verification
- `src/shared/Angor.Shared/Protocol/FounderTransactionActions.cs` — uses `TaprootKeyHelper` for sig verification

## Diagnostic Code

The following diagnostic code was used to isolate the bug. It can be added to any class
and called at app startup to verify taproot key generation on the current runtime:

```csharp
private static void DiagLog(string message)
{
    try
    {
        var path = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "taproot-diag.log");
        File.AppendAllText(path, $"{DateTime.UtcNow:O} {message}{Environment.NewLine}");
    }
    catch { /* best effort */ }
}

/// <summary>
/// Diagnostic: test GetTaprootFullPubKey on a known pubkey to verify secp256k1 works
/// on this runtime. Call this at app startup.
/// </summary>
public static void RunDiagnostics()
{
    try
    {
        DiagLog("=== RunDiagnostics START ===");

        // Use the secp256k1 generator point as a well-known test pubkey
        var testPubKeyHex = "0279BE667EF9DCBBAC55A06295CE870B07029BFCDB2DCE28D959F2815B16F81798";
        var pubkey = new NBitcoin.PubKey(testPubKeyHex);
        DiagLog($"Test PubKey created: {pubkey.ToHex()}");

        // Test 1: NBitcoin's GetTaprootFullPubKey (broken on .NET 10 ARM64)
        var taprootFullPubKey = pubkey.GetTaprootFullPubKey();
        var taprootBytes = taprootFullPubKey.ToBytes();
        DiagLog($"GetTaprootFullPubKey: {Convert.ToHexString(taprootBytes)}");
        DiagLog($"All-zero: {taprootBytes.All(b => b == 0)}");

        // Test 2: Low-level ECPubKey operations
        var ctx = NBitcoin.Secp256k1.Context.Instance;
        var pubBytes = pubkey.ToBytes();
        NBitcoin.Secp256k1.ECPubKey.TryCreate(pubBytes, ctx, out var compressed, out var ecpk);
        DiagLog($"ECPubKey.TryCreate: success={ecpk != null}");

        if (ecpk != null)
        {
            var xonly = ecpk.ToXOnlyPubKey(out bool parity);
            var xonlyBytes = new byte[32];
            xonly.WriteToSpan(xonlyBytes);
            DiagLog($"ToXOnlyPubKey: {Convert.ToHexString(xonlyBytes)} parity={parity}");

            // Test 3: ComputeTapTweak (works correctly on all platforms)
            var tipk = new NBitcoin.TaprootInternalPubKey(xonlyBytes);
            var tweak = tipk.ComputeTapTweak(null);
            DiagLog($"ComputeTapTweak: {Convert.ToHexString(tweak)}");

            // Test 4: TryAddTweak called from OUR assembly (works correctly)
            var tweakSuccess = xonly.TryAddTweak(tweak, out var tweakedPubKey);
            DiagLog($"TryAddTweak (our assembly): success={tweakSuccess}");
            if (tweakedPubKey != null)
            {
                var tweakedBytes = new byte[32];
                tweakedPubKey.ToXOnlyPubKey(out _).WriteToSpan(tweakedBytes);
                DiagLog($"Tweaked key: {Convert.ToHexString(tweakedBytes)}");
            }

            // Test 5: GetTaprootFullPubKey (broken — calls AddTweak from NBitcoin.dll)
            var fullPk = tipk.GetTaprootFullPubKey(null);
            DiagLog($"GetTaprootFullPubKey(null): {Convert.ToHexString(fullPk.ToBytes())}");

            // Test 6: Check tweak stored in result (correct even when output is zero)
            var storedTweak = fullPk.Tweak;
            DiagLog($"Stored tweak: {Convert.ToHexString(storedTweak.Span)}");
            DiagLog($"Tweak matches: {storedTweak.Span.SequenceEqual(tweak)}");

            // Test 7: Simple tweak=1 sanity check (2*G = P + G)
            var simpleTweak = new byte[32];
            simpleTweak[31] = 1;
            xonly.TryAddTweak(simpleTweak, out var simpleTweaked);
            if (simpleTweaked != null)
            {
                var stBytes = new byte[32];
                simpleTweaked.ToXOnlyPubKey(out _).WriteToSpan(stBytes);
                DiagLog($"AddTweak(1) = {Convert.ToHexString(stBytes)}");
                DiagLog($"Expected 2G: C6047F9441ED7D6D3045406E95C07CD85C778E4B8CEF3CA7ABAC09B95C709EE5");
            }
        }

        DiagLog($"Secp256k1: {typeof(NBitcoin.Secp256k1.ECPubKey).Assembly.FullName}");
        DiagLog($"Runtime: {System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription}");
        DiagLog($"OS: {System.Runtime.InteropServices.RuntimeInformation.OSDescription}");
        DiagLog($"Arch: {System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture}");
        DiagLog("=== RunDiagnostics END ===");
    }
    catch (Exception ex)
    {
        DiagLog($"RunDiagnostics EXCEPTION: {ex}");
    }
}
```

### Sample output (Android ARM64, .NET 10.0.7)

```
=== RunDiagnostics START ===
Test PubKey created: 0279be667ef9dcbbac55a06295ce870b07029bfcdb2dce28d959f2815b16f81798
GetTaprootFullPubKey: 0000000000000000000000000000000000000000000000000000000000000000
All-zero: True
ECPubKey.TryCreate: success=True
ToXOnlyPubKey: 79BE667EF9DCBBAC55A06295CE870B07029BFCDB2DCE28D959F2815B16F81798 parity=False
ComputeTapTweak: 3CF5216D476A5E637BF0DA674E50DDF55C403270DD36494DFCCA438132FA30E7
TryAddTweak (our assembly): success=True
Tweaked key: DA4710964F7852695DE2DA025290E24AF6D8C281DE5A0B902B7135FD9FD74D21
GetTaprootFullPubKey(null): 0000000000000000000000000000000000000000000000000000000000000000
Stored tweak: 3CF5216D476A5E637BF0DA674E50DDF55C403270DD36494DFCCA438132FA30E7
Tweak matches: True
AddTweak(1) = C6047F9441ED7D6D3045406E95C07CD85C778E4B8CEF3CA7ABAC09B95C709EE5
Expected 2G: C6047F9441ED7D6D3045406E95C07CD85C778E4B8CEF3CA7ABAC09B95C709EE5
Secp256k1: NBitcoin.Secp256k1, Version=3.2.0.0, Culture=neutral, PublicKeyToken=null
Runtime: .NET 10.0.7
OS: Android (API level 33)
Arch: Arm64
=== RunDiagnostics END ===
```

Note how `TryAddTweak` called from Angor's assembly produces the correct result
(`DA4710...`), but `GetTaprootFullPubKey` (which calls `AddTweak` from inside
NBitcoin.dll) returns all zeros — with the **same tweak bytes**.
