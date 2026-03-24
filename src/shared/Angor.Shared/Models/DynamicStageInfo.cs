using Angor.Shared.Utilities;

namespace Angor.Shared.Models;

/// <summary>
/// Represents encoded dynamic stage information for compact storage in OP_RETURN scripts.
/// Total size: 4 bytes (can fit in a single OP_RETURN push data)
/// </summary>
public class DynamicStageInfo
{
    /// <summary>
    /// Days since epoch (Jan 1, 2025) for the investment start date.
    /// 2 bytes = supports dates up to ~179 years from epoch.
    /// </summary>
    public ushort InvestmentStartDaysSinceEpoch { get; set; }

    /// <summary>
    /// Pattern identifier (0-255).
    /// References a DynamicStagePattern in the project.
    /// </summary>
    public byte PatternId { get; set; }

    /// <summary>
    /// Number of stages (1-255).
    /// </summary>
    public byte StageCount { get; set; }

    /// <summary>
    /// Encodes the dynamic stage info into a 4-byte array for OP_RETURN.
    /// Format: [2 bytes: start days] [1 byte: pattern] [1 byte: count]
    /// </summary>
    public byte[] ToBytes()
    {
        var bytes = new byte[4];

        // Bytes 0-1: Investment start date (days since epoch) - little endian
        bytes[0] = (byte)(InvestmentStartDaysSinceEpoch & 0xFF);
        bytes[1] = (byte)((InvestmentStartDaysSinceEpoch >> 8) & 0xFF);

        // Byte 2: Pattern ID
        bytes[2] = PatternId;

        // Byte 3: Stage count
        bytes[3] = StageCount;

        return bytes;
    }

    /// <summary>
    /// Decodes dynamic stage info from a byte array.
    /// </summary>
    /// <param name="bytes">4-byte array containing encoded stage info</param>
    /// <returns>Decoded DynamicStageInfo</returns>
    /// <exception cref="ArgumentException">If bytes array is not exactly 4 bytes</exception>
    public static DynamicStageInfo FromBytes(byte[] bytes)
    {
        if (bytes == null || bytes.Length != 4)
        {
            throw new ArgumentException("Dynamic stage info must be exactly 4 bytes", nameof(bytes));
        }

        return new DynamicStageInfo
        {
            InvestmentStartDaysSinceEpoch = (ushort)(bytes[0] | (bytes[1] << 8)),
            PatternId = bytes[2],
            StageCount = bytes[3]
        };
    }

    /// <summary>
    /// Creates DynamicStageInfo from a DynamicStagePattern and investment start date.
    /// </summary>
    public static DynamicStageInfo FromPattern(DynamicStagePattern pattern, FundingParameters fundingParameters)
    {
        return new DynamicStageInfo
        {
            InvestmentStartDaysSinceEpoch = DynamicStageHelper.ToDaysSinceEpoch(fundingParameters.InvestmentStartDate.Value),
            PatternId = fundingParameters.PatternId,
            StageCount = fundingParameters.StageCountOverride > 0 ? (byte)fundingParameters.StageCountOverride : (byte)0
        };
    }

    /// <summary>
    /// Gets the investment start date from the encoded value.
    /// </summary>
    public DateTime GetInvestmentStartDate()
    {
        return DynamicStageHelper.FromDaysSinceEpoch(InvestmentStartDaysSinceEpoch);
    }
}
