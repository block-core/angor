using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using FluentAssertions;
using Xunit;

namespace App.Test.Integration.LayoutRegression;

/// <summary>
/// Source-scan guard for the semantic-colour migration: danger/warning/Bitcoin accent
/// hexes must live ONLY in UI/Themes resource files as named tokens (DangerBrush,
/// WarningBrush, BitcoinAccentBrush, …). Views must reference the tokens — never the raw
/// hex — so the semantic meaning can be re-tinted globally and the palette stays
/// theme-aware. A literal here silently forks the palette; this test fails listing
/// every file:line offender. (First source-scan test in the suite; complements the
/// rendering-based layout/icon tests.)
/// </summary>
public class HexLiteralGuardTests
{
    // The literals migrated in the semantic-token pass (case-insensitive).
    private static readonly string[] BannedHexes =
    {
        "DC2626",   // danger red (strong)  → DangerBrush
        "EF4444",   // danger red (bright)  → DangerBrightBrush
        "1ADC2626", // danger soft fill     → DangerSoftBrush
        "F97316",   // warning orange       → WarningBrush
        "FF8C00",   // warning bright       → WarningBrightBrush
        "30FF8C00", // warning soft fill    → WarningSoftBrush
        "F7931A",   // Bitcoin orange       → BitcoinAccentBrush / BitcoinOrange (Color)
    };

    private static readonly Regex BannedAttribute =
        new("""
            (Foreground|Background|BorderBrush|Stroke|Fill|Color)\s*=\s*"#(?:DC2626|EF4444|1ADC2626|F97316|FF8C00|30FF8C00|F7931A)"
            """,
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

    [Fact]
    public void Views_do_not_use_banned_hex_literals()
    {
        var uiDir = FindUiDirectory();
        var scannedDirs = new[] { "Sections", "Shell", "Shared" };

        var offenders = new List<string>();
        foreach (var dir in scannedDirs)
        {
            foreach (var file in Directory.EnumerateFiles(
                         Path.Combine(uiDir, dir), "*.axaml", SearchOption.AllDirectories))
            {
                var lines = File.ReadAllLines(file);
                for (var i = 0; i < lines.Length; i++)
                {
                    if (BannedAttribute.IsMatch(lines[i]))
                        offenders.Add($"{file}:{i + 1}: {lines[i].Trim()}");
                }
            }
        }

        offenders.Should().BeEmpty(
            "use the semantic tokens from Colors.Core.axaml instead of literal hexes:\n" +
            string.Join("\n", offenders));
    }

    private static string FindUiDirectory()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            var candidate = Path.Combine(dir.FullName, "src", "design", "App", "UI");
            if (Directory.Exists(candidate))
                return candidate;
            dir = dir.Parent;
        }

        throw new DirectoryNotFoundException(
            $"Could not locate src/design/App/UI walking up from {AppContext.BaseDirectory}");
    }
}
