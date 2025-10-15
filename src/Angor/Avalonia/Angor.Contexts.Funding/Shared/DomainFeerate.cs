namespace Angor.Contexts.Funding.Shared;

public record DomainFeerate(long SatsPerVByte)
{
    public long SatsPerKilobyte => SatsPerVByte * 1000;
}