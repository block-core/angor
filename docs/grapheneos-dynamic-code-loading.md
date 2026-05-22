# GrapheneOS Compatibility: Dynamic Code Loading

## Problem

GrapheneOS enforces strict W^X (Write XOR Execute) memory protection as part of its exploit hardening. This blocks apps from generating executable machine code in memory at runtime. The Angor Android app currently relies on this capability, making it incompatible with GrapheneOS's default security settings.

Users on GrapheneOS will see the app crash or fail to start unless they manually disable "Exploit Protection" for the app — which defeats the purpose of running a hardened OS.

## Root Cause

The .NET Android runtime (Mono) uses a JIT (Just-In-Time) compiler by default: IL bytecode is compiled to native machine code in memory at runtime. GrapheneOS blocks this because runtime code generation is a common exploitation technique.

## What Needs to Change

### 1. Enable Full AOT Compilation

Both Android projects have AOT explicitly disabled:

**`src/design/App.Android/App.Android.csproj`** (lines 11, 20, 23):
```xml
<AndroidEnableProfiledAot>false</AndroidEnableProfiledAot>
<PublishAot>false</PublishAot>
<RunAOTCompilation>false</RunAOTCompilation>
```

**`src/avalonia/AngorApp.Android/AngorApp.Android.csproj`** (lines 11, 19, 22):
```xml
<AndroidEnableProfiledAot>false</AndroidEnableProfiledAot>
<PublishAot>false</PublishAot>
<RunAOTCompilation>false</RunAOTCompilation>
```

These need to be set to `true` (at least `RunAOTCompilation`) so all code is compiled to native code at build time rather than at runtime.

### 2. Remove `Expression.Compile()` Usage

`Expression.Compile()` generates executable code at runtime. These call sites must be rewritten:

- **`src/shared/Angor.Shared/Protocol/Scripts/TaprootScriptBuilder.cs:31`** — compiles a LINQ expression tree to select a taproot script. Replace with a direct method call or delegate.
- **`src/sdk/Angor.Data.Documents.LiteDb/LiteDbGenericDocumentCollection.cs:50,60,67`** — compiles expression trees to extract document IDs. Replace with direct function parameters (`Func<T, TId>`) instead of `Expression<Func<T, TId>>`.

### 3. Address Reflection-Based Generic Construction

- **`src/sdk/Angor.Sdk/Common/MediatR/UnhandledExceptionBehavior.cs:52`** — uses `MakeGenericMethod().Invoke()` to construct `Result.Failure<T>()` at runtime. This creates new generic instantiations that may not exist in the AOT-compiled binary. Refactor to avoid runtime generic construction (e.g., use a non-generic `Result.Failure()` overload or a type switch for known payload types).

### 4. Verify MediatR Compatibility with AOT

- **`src/sdk/Angor.Sdk/Funding/FundingContextServices.cs:40`** — `RegisterServicesFromAssembly()` uses reflection to scan for handlers. This should work under AOT (it reads metadata, not generates code), but needs verification. MediatR 12.x has known AOT limitations; check if source-generated registration is available.

### 5. Add `[RequiresDynamicCode]` Annotations

The codebase currently has zero `[RequiresDynamicCode]` or `[RequiresUnreferencedCode]` annotations, and both Android projects suppress IL2026/IL2111 warnings. Once AOT is enabled, these warnings should be un-suppressed and addressed properly rather than hidden.

## Verification

After making changes:

1. Build with `RunAOTCompilation=true` and confirm no AOT warnings about dynamic code
2. Test on a GrapheneOS device (or emulator with hardened memory policy) with default exploit protection enabled
3. Verify taproot script building, LiteDB document operations, and MediatR handler resolution all work correctly

## References

- [GrapheneOS Exploit Protection](https://grapheneos.org/usage#exploit-protection)
- [.NET Android AOT Documentation](https://learn.microsoft.com/en-us/dotnet/android/building-apps/build-properties#runaotcompilation)
- [.NET AOT Compatibility Guide](https://learn.microsoft.com/en-us/dotnet/core/deploying/native-aot/?tabs=net8plus)
