using System.Threading;

namespace App.UI.Shared.Services;

public interface IBrantaSendValidationService
{
    Task<BrantaSendValidationResult> ValidateAsync(string destination, CancellationToken ct = default);
}

public sealed record BrantaSendValidationResult(bool IsValid, string? ErrorMessage = null);
