using System.Diagnostics.CodeAnalysis;
using Serilog.Core;
using Serilog.Events;

namespace App.Composition;

/// <summary>
/// Serilog destructuring policy that redacts structured log properties whose names
/// match known sensitive patterns (passwords, seeds, private keys, etc.).
/// This prevents sensitive data from reaching log files at write time.
/// </summary>
public class SensitiveDataRedactor : IDestructuringPolicy
{
    private static readonly HashSet<string> SensitiveNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "Password",
        "Seed",
        "Mnemonic",
        "PrivateKey",
        "Nsec",
        "Secret",
        "Passphrase",
        "WalletWords",
        "SeedWords",
        "Xprv",
        "Tprv",
        "Key",
    };

    public bool TryDestructure(
        object value,
        ILogEventPropertyValueFactory propertyValueFactory,
        [NotNullWhen(true)] out LogEventPropertyValue? result)
    {
        result = null;

        if (value is not IReadOnlyDictionary<string, object> dict)
            return false;

        var properties = new List<LogEventProperty>();
        var redacted = false;

        foreach (var kvp in dict)
        {
            if (SensitiveNames.Contains(kvp.Key))
            {
                properties.Add(new LogEventProperty(kvp.Key, new ScalarValue("[REDACTED]")));
                redacted = true;
            }
            else
            {
                properties.Add(new LogEventProperty(kvp.Key, propertyValueFactory.CreatePropertyValue(kvp.Value, true)));
            }
        }

        if (!redacted)
            return false;

        result = new StructureValue(properties);
        return true;
    }
}
