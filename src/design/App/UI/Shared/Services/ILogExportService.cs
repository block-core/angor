using System.Threading;
using CSharpFunctionalExtensions;

namespace App.UI.Shared.Services;

public interface ILogExportService
{
    Task<Result> ExportAndSendAsync(string walletId, CancellationToken ct = default);
}
