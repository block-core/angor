using System;

namespace AngorApp.UI.Flows.Invest.Amount;

/// <summary>
/// Converts between ListBox SelectedIndex (int) and SelectedPatternIndex (byte?)
/// </summary>
public static class PatternIndexConverter
{
    /// <summary>
    /// Converts an integer index to a nullable byte for pattern selection.
    /// Returns null if index is -1 (no selection).
    /// </summary>
    public static byte? IntToNullableByte(int index)
    {
        if (index < 0)
            return null;

        if (index > 255)
            throw new ArgumentOutOfRangeException(nameof(index), "Pattern index cannot exceed 255");

        return (byte)index;
    }

    /// <summary>
    /// Converts a nullable byte pattern index back to an integer for ListBox SelectedIndex.
    /// Returns -1 if the value is null (no selection).
    /// </summary>
    public static int NullableByteToInt(byte? patternIndex)
    {
        return patternIndex ?? -1;
    }
}
