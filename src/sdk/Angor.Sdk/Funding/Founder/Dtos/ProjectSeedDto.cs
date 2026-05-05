// File: `Angor/Contexts/Funding/Founder/Dtos/ProjectSeedDto.cs`
using Angor.Primitives;

namespace Angor.Sdk.Funding.Founder.Dtos;

public record ProjectSeedDto(
    string FounderKey,
    string FounderRecoveryKey,
    string NostrPubKey,
    string ProjectIdentifier);