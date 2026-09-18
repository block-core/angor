using System.Globalization;
using NBitcoin;

namespace App.UI.Sections.Funds;

public sealed record BitcoinQrPayment(string Address, decimal? AmountBtc);

public static class BitcoinQrPaymentParser
{
    public static bool TryParse(string? content, out BitcoinQrPayment? payment, out string? error)
    {
        payment = null;
        error = null;

        // Some wallet apps embed zero-width/format characters or newlines in QR
        // payloads — strip them before validation, otherwise valid addresses are
        // rejected as invalid.
        string value = new string((content ?? string.Empty)
            .Where(c => !char.IsControl(c) && char.GetUnicodeCategory(c) != UnicodeCategory.Format)
            .ToArray()).Trim();

        if (value.Length == 0)
        {
            error = "The QR code is empty.";
            return false;
        }

        if (!value.StartsWith("bitcoin:", StringComparison.OrdinalIgnoreCase))
        {
            if (value.Contains(':') || value.Contains('?') || value.Any(char.IsWhiteSpace))
            {
                error = "This QR code does not contain a Bitcoin address.";
                return false;
            }

            if (!IsBitcoinAddress(value))
            {
                error = "This QR code does not contain a valid Bitcoin address.";
                return false;
            }

            payment = new BitcoinQrPayment(value, null);
            return true;
        }

        string uri = value["bitcoin:".Length..];
        int queryIndex = uri.IndexOf('?');
        string address = Uri.UnescapeDataString(queryIndex >= 0 ? uri[..queryIndex] : uri);
        if (string.IsNullOrWhiteSpace(address))
        {
            error = "The Bitcoin payment request has no address.";
            return false;
        }

        if (!IsBitcoinAddress(address))
        {
            error = "The Bitcoin payment request contains an invalid address.";
            return false;
        }

        decimal? amount = null;
        if (queryIndex >= 0)
        {
            foreach (string pair in uri[(queryIndex + 1)..].Split('&', StringSplitOptions.RemoveEmptyEntries))
            {
                string[] parts = pair.Split('=', 2);
                string key = Uri.UnescapeDataString(parts[0]);
                string parameterValue = parts.Length == 2 ? Uri.UnescapeDataString(parts[1]) : string.Empty;

                if (key.StartsWith("req-", StringComparison.OrdinalIgnoreCase))
                {
                    error = $"Unsupported required Bitcoin payment parameter: {key}.";
                    return false;
                }

                if (!key.Equals("amount", StringComparison.OrdinalIgnoreCase))
                    continue;

                if (!decimal.TryParse(parameterValue, NumberStyles.AllowDecimalPoint,
                        CultureInfo.InvariantCulture, out decimal parsedAmount) || parsedAmount <= 0)
                {
                    error = "The Bitcoin payment request contains an invalid amount.";
                    return false;
                }

                if (((decimal.GetBits(parsedAmount)[3] >> 16) & 0x7F) > 8)
                {
                    error = "The Bitcoin payment amount has more than 8 decimal places.";
                    return false;
                }

                amount = parsedAmount;
            }
        }

        payment = new BitcoinQrPayment(address, amount);
        return true;
    }

    private static bool IsBitcoinAddress(string address)
    {
        return TryAddress(address, Network.Main) || TryAddress(address, Network.TestNet) ||
            TryAddress(address, Network.RegTest);
    }

    private static bool TryAddress(string address, Network network)
    {
        try
        {
            BitcoinAddress.Create(address, network);
            return true;
        }
        catch (Exception ex) when (ex is FormatException or ArgumentException)
        {
            return false;
        }
    }
}
