using Angor.Sdk.Common;
using Angor.Sdk.Wallet.Infrastructure.Interfaces;
using CSharpFunctionalExtensions;

namespace Angor.Cli.Composition;

/// <summary>
/// Provides wallet password from the secure key provider (DPAPI on Windows, keyring on Linux),
/// falling back to environment variable ANGOR_WALLET_PASSWORD.
/// In CLI interactive mode, falls back further to console prompt.
/// In MCP mode, fails with a clear error if neither source is available.
/// </summary>
public class HeadlessPasswordProvider(bool isMcpMode, ISecureKeyProvider secureKeyProvider) : IPasswordProvider
{
    public async Task<Maybe<string>> Get(WalletId walletId)
    {
        // First try: DPAPI / secure key store (contains the actual encryption key)
        var secureKey = await secureKeyProvider.Get(walletId);
        if (secureKey.HasValue)
        {
            return secureKey;
        }

        // Second try: environment variable
        var envPassword = Environment.GetEnvironmentVariable("ANGOR_WALLET_PASSWORD");
        if (!string.IsNullOrEmpty(envPassword))
        {
            return Maybe<string>.From(envPassword);
        }

        if (isMcpMode)
        {
            // In MCP mode, stdin is reserved for JSON-RPC — cannot prompt interactively.
            return Maybe<string>.None;
        }

        // CLI interactive mode — prompt on console.
        Console.Error.Write($"Enter password for wallet '{walletId}': ");
        var password = ReadPasswordFromConsole();
        if (string.IsNullOrEmpty(password))
        {
            return Maybe<string>.None;
        }

        return Maybe<string>.From(password);
    }

    private static string ReadPasswordFromConsole()
    {
        var password = new System.Text.StringBuilder();
        while (true)
        {
            var key = Console.ReadKey(intercept: true);
            if (key.Key == ConsoleKey.Enter)
            {
                Console.Error.WriteLine();
                break;
            }

            if (key.Key == ConsoleKey.Backspace && password.Length > 0)
            {
                password.Remove(password.Length - 1, 1);
                Console.Error.Write("\b \b");
            }
            else if (!char.IsControl(key.KeyChar))
            {
                password.Append(key.KeyChar);
                Console.Error.Write('*');
            }
        }

        return password.ToString();
    }
}
