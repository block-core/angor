using Angor.Shared.Utilities;

namespace Angor.Shared.Models;

/// <summary>
/// Represents encoded dynamic stage information for compact storage in OP_RETURN scripts.
/// Total size: 7 bytes (can fit in a single OP_RETURN push data)
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
    /// Stage frequency (0-255).
    /// Maps to StageFrequency enum.
    /// </summary>
    public byte Frequency { get; set; }

    /// <summary>
    /// Payout day type (0-255).
    /// Maps to PayoutDayType enum.
    /// </summary>
    public byte PayoutDayType { get; set; }

    /// <summary>
    /// Payout day value (0-255).
    /// Interpretation depends on PayoutDayType:
    /// - FromStartDate: Not used (0)
    /// - SpecificDayOfMonth: Day of month (1-31)
    /// - SpecificDayOfWeek: Day of week (0=Sunday, 6=Saturday)
    /// </summary>
    public byte PayoutDay { get; set; }

    /// <summary>
    /// Encodes the dynamic stage info into a 7-byte array for OP_RETURN.
    /// Format: [2 bytes: start days] [1 byte: pattern] [1 byte: count] [1 byte: freq] [1 byte: payout type] [1 byte: payout day]
    /// </summary>
    public byte[] ToBytes()
    {
        var bytes = new byte[7];

        // Bytes 0-1: Investment start date (days since epoch) - little endian
        bytes[0] = (byte)(InvestmentStartDaysSinceEpoch & 0xFF);
        bytes[1] = (byte)((InvestmentStartDaysSinceEpoch >> 8) & 0xFF);

        // Byte 2: Pattern ID
        bytes[2] = PatternId;

        // Byte 3: Stage count
        bytes[3] = StageCount;

        // Byte 4: Frequency
        bytes[4] = Frequency;

        // Byte 5: Payout day type
        bytes[5] = PayoutDayType;

        // Byte 6: Payout day
        bytes[6] = PayoutDay;

        return bytes;
    }

    /// <summary>
    /// Decodes dynamic stage info from a byte array.
    /// </summary>
    /// <param name="bytes">7-byte array containing encoded stage info</param>
    /// <returns>Decoded DynamicStageInfo</returns>
    /// <exception cref="ArgumentException">If bytes array is not exactly 7 bytes</exception>
    public static DynamicStageInfo FromBytes(byte[] bytes)
    {
        if (bytes == null || bytes.Length != 7)
        {
            throw new ArgumentException("Dynamic stage info must be exactly 7 bytes", nameof(bytes));
        }

        return new DynamicStageInfo
        {
            InvestmentStartDaysSinceEpoch = (ushort)(bytes[0] | (bytes[1] << 8)),
            PatternId = bytes[2],
            StageCount = bytes[3],
            Frequency = bytes[4],
            PayoutDayType = bytes[5],
            PayoutDay = bytes[6]
        };
    }

    /// <summary>
    /// Creates DynamicStageInfo from a DynamicStagePattern and investment start date.
    /// </summary>
    public static DynamicStageInfo FromPattern(DynamicStagePattern pattern, DateTime investmentStartDate, byte patternIndex = 0)
    {
        return new DynamicStageInfo
        {
            InvestmentStartDaysSinceEpoch = DynamicStageHelper.ToDaysSinceEpoch(investmentStartDate),
            PatternId = patternIndex,
            StageCount = (byte)pattern.StageCount,
            Frequency = (byte)pattern.Frequency,
            PayoutDayType = (byte)pattern.PayoutDayType,
            PayoutDay = (byte)pattern.PayoutDay
        };
    }

    /// <summary>
    /// Converts to a DynamicStagePattern (without PatternId, Name, Description).
    /// </summary>
    public DynamicStagePattern ToPattern()
    {
        return new DynamicStagePattern
        {
            StageCount = StageCount,
            Frequency = (StageFrequency)Frequency,
            PayoutDayType = (Models.PayoutDayType)PayoutDayType,
            PayoutDay = PayoutDay
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
