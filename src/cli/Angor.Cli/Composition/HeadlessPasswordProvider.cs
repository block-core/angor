using Angor.Sdk.Common;
using Angor.Sdk.Wallet.Infrastructure.Interfaces;
using CSharpFunctionalExtensions;

namespace Angor.Cli.Composition;

/// <summary>
/// Provides wallet password from environment variable ANGOR_WALLET_PASSWORD.
/// In CLI interactive mode, falls back to console prompt.
/// In MCP mode, fails with a clear error if the env var is not set.
/// </summary>
public class HeadlessPasswordProvider(bool isMcpMode) : IPasswordProvider
{
    public Task<Maybe<string>> Get(WalletId walletId)
    {
        var envPassword = Environment.GetEnvironmentVariable("ANGOR_WALLET_PASSWORD");
        if (!string.IsNullOrEmpty(envPassword))
        {
            return Task.FromResult(Maybe<string>.From(envPassword));
        }

        if (isMcpMode)
        {
            // In MCP mode, stdin is reserved for JSON-RPC — cannot prompt interactively.
            return Task.FromResult(Maybe<string>.None);
        }

        // CLI interactive mode — prompt on console.
        Console.Error.Write($"Enter password for wallet '{walletId}': ");
        var password = ReadPasswordFromConsole();
        if (string.IsNullOrEmpty(password))
        {
            return Task.FromResult(Maybe<string>.None);
        }

        return Task.FromResult(Maybe<string>.From(password));
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
