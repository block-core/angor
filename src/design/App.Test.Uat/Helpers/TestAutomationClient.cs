using System.Net.Http;
using System.Text;
using System.Text.Json;
using static App.Automation.AutomationDtos;
using static App.Automation.AutomationFlowDtos;

namespace App.Test.Uat.Helpers;

/// <summary>
/// Typed HTTP client for the App test automation server.
/// Wraps the JSON-over-HTTP protocol exposed by AutomationServer.
/// </summary>
public sealed class TestAutomationClient : IDisposable
{
    private readonly HttpClient http;
    private readonly string baseUrl;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public TestAutomationClient(string baseUrl)
    {
        this.baseUrl = baseUrl.TrimEnd('/');
        http = new HttpClient { Timeout = TimeSpan.FromMinutes(15) };
    }

    // ═══════════════════════════════════════════════════════════════════
    // Health
    // ═══════════════════════════════════════════════════════════════════

    public async Task<bool> HealthCheckAsync(CancellationToken ct = default)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(TimeSpan.FromSeconds(5));
        var response = await http.GetAsync($"{baseUrl}/health", cts.Token);
        if (!response.IsSuccessStatusCode) return false;
        var body = await response.Content.ReadAsStringAsync(cts.Token);
        var result = JsonSerializer.Deserialize<HealthResponse>(body, JsonOptions);
        return result?.Ready == true;
    }

    // ═══════════════════════════════════════════════════════════════════
    // Navigation
    // ═══════════════════════════════════════════════════════════════════

    public async Task NavigateAsync(string section, CancellationToken ct = default)
    {
        var result = await PostAsync<ActionResponse>("/navigate",
            new NavigateRequest { Section = section }, ct);
        if (!result.Success)
        {
            throw new InvalidOperationException($"Navigate to '{section}' failed: {result.Error}");
        }

        await Task.Delay(500, ct);
    }

    // ═══════════════════════════════════════════════════════════════════
    // Control Interaction
    // ═══════════════════════════════════════════════════════════════════

    public async Task<ControlInfo> FindControlAsync(string automationId, CancellationToken ct = default)
    {
        return await GetAsync<ControlInfo>($"/control/{Uri.EscapeDataString(automationId)}", ct);
    }

    public async Task ClickAsync(string automationId, CancellationToken ct = default)
    {
        var result = await PostAsync<ActionResponse>("/click",
            new ClickRequest { AutomationId = automationId }, ct);
        if (!result.Success)
        {
            throw new InvalidOperationException($"Click '{automationId}' failed: {result.Error}");
        }
    }

    public async Task TypeTextAsync(string automationId, string text, CancellationToken ct = default)
    {
        var result = await PostAsync<ActionResponse>("/type-text",
            new TypeTextRequest { AutomationId = automationId, Text = text }, ct);
        if (!result.Success)
        {
            throw new InvalidOperationException($"TypeText '{automationId}' failed: {result.Error}");
        }
    }

    public async Task<ControlInfo> WaitForControlAsync(
        string automationId,
        TimeSpan? timeout = null,
        bool visible = true,
        CancellationToken ct = default)
    {
        var timeoutMs = (int)(timeout ?? TimeSpan.FromSeconds(15)).TotalMilliseconds;
        var result = await PostAsync<ControlInfo>("/wait-for-control",
            new WaitForControlRequest
            {
                AutomationId = automationId,
                TimeoutMs = timeoutMs,
                Visible = visible,
            }, ct);

        if (!result.Found)
        {
            throw new TimeoutException($"Control '{automationId}' not found within {timeoutMs}ms");
        }

        return result;
    }

    // ═══════════════════════════════════════════════════════════════════
    // ViewModel Access
    // ═══════════════════════════════════════════════════════════════════

    public async Task<string?> GetVmPropertyAsync(string vmTypeName, string property, CancellationToken ct = default)
    {
        var result = await GetAsync<ValueResponse>(
            $"/vm/{Uri.EscapeDataString(vmTypeName)}/{Uri.EscapeDataString(property)}", ct);

        if (result.Error != null)
        {
            throw new InvalidOperationException($"GetVmProperty '{vmTypeName}.{property}' failed: {result.Error}");
        }

        return result.Value?.ToString();
    }

    public async Task InvokeVmAsync(string vmTypeName, string method, CancellationToken ct = default)
    {
        var result = await PostAsync<ActionResponse>("/vm/invoke",
            new VmInvokeRequest { Type = vmTypeName, Method = method }, ct);

        if (!result.Success)
        {
            throw new InvalidOperationException($"InvokeVm '{vmTypeName}.{method}' failed: {result.Error}");
        }
    }

    public async Task SetVmPropertyAsync(string vmTypeName, string property, object? value, CancellationToken ct = default)
    {
        var result = await PostAsync<ActionResponse>("/vm/set-property",
            new SetVmPropertyRequest { Type = vmTypeName, Property = property, Value = value }, ct);

        if (!result.Success)
        {
            throw new InvalidOperationException($"SetVmProperty '{vmTypeName}.{property}' failed: {result.Error}");
        }
    }

    // ═══════════════════════════════════════════════════════════════════
    // Convenience: Wait for VM property
    // ═══════════════════════════════════════════════════════════════════

    public async Task WaitForVmPropertyAsync(
        string vmTypeName,
        string property,
        string expectedValue,
        TimeSpan? timeout = null,
        TimeSpan? pollInterval = null,
        CancellationToken ct = default)
    {
        var deadline = DateTime.UtcNow + (timeout ?? TimeSpan.FromMinutes(1));
        var interval = pollInterval ?? TimeSpan.FromSeconds(2);

        while (DateTime.UtcNow < deadline)
        {
            ct.ThrowIfCancellationRequested();

            try
            {
                var value = await GetVmPropertyAsync(vmTypeName, property, ct);
                if (string.Equals(value, expectedValue, StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }
            }
            catch
            {
                // Property may not be available yet
            }

            await Task.Delay(interval, ct);
        }

        throw new TimeoutException(
            $"VM property '{vmTypeName}.{property}' did not become '{expectedValue}' within timeout");
    }

    // ═══════════════════════════════════════════════════════════════════
    // Setup
    // ═══════════════════════════════════════════════════════════════════

    public async Task WipeDataAsync(CancellationToken ct = default)
    {
        var result = await PostAsync<ActionResponse>("/wipe", (object?)null, ct);
        if (!result.Success)
        {
            throw new InvalidOperationException($"WipeData failed: {result.Error}");
        }

        await Task.Delay(500, ct);
    }

    public async Task EnableDebugModeAsync(CancellationToken ct = default)
    {
        var result = await PostAsync<ActionResponse>("/debug-mode", (object?)null, ct);
        if (!result.Success)
        {
            throw new InvalidOperationException($"EnableDebugMode failed: {result.Error}");
        }
    }

    // ═══════════════════════════════════════════════════════════════════
    // Composite Flows
    // ═══════════════════════════════════════════════════════════════════

    public async Task<CreateWalletAndFundResponse> CreateWalletAndFundAsync(
        CreateWalletAndFundRequest request,
        CancellationToken ct = default)
    {
        return await PostAsync<CreateWalletAndFundResponse>("/flows/create-wallet-and-fund", request, ct);
    }

    public async Task<ProjectCreatedResponse> CreateFundProjectAsync(
        CreateFundProjectRequest request,
        CancellationToken ct = default)
    {
        return await PostAsync<ProjectCreatedResponse>("/flows/create-fund-project", request, ct);
    }

    public async Task<ProjectCreatedResponse> CreateInvestProjectAsync(
        CreateInvestProjectRequest request,
        CancellationToken ct = default)
    {
        return await PostAsync<ProjectCreatedResponse>("/flows/create-invest-project", request, ct);
    }

    public async Task<InvestResponse> InvestInProjectAsync(
        InvestInProjectRequest request,
        CancellationToken ct = default)
    {
        return await PostAsync<InvestResponse>("/flows/invest", request, ct);
    }

    public async Task<ApproveInvestmentsResponse> ApproveInvestmentsAsync(
        ApproveInvestmentsRequest request,
        CancellationToken ct = default)
    {
        return await PostAsync<ApproveInvestmentsResponse>("/flows/approve-investments", request, ct);
    }

    public async Task<ConfirmInvestmentResponse> ConfirmInvestmentAsync(
        ConfirmInvestmentRequest request,
        CancellationToken ct = default)
    {
        return await PostAsync<ConfirmInvestmentResponse>("/flows/confirm-investment", request, ct);
    }

    public async Task<ActionResponse> ClaimStageAsync(
        ClaimStageRequest request,
        CancellationToken ct = default)
    {
        return await PostAsync<ActionResponse>("/flows/claim-stage", request, ct);
    }

    public async Task<RecoveryResponse> ExecuteRecoveryAsync(
        RecoveryRequest request,
        CancellationToken ct = default)
    {
        return await PostAsync<RecoveryResponse>("/flows/recovery", request, ct);
    }

    public async Task<ActionResponse> ReleaseFundsToInvestorsAsync(
        ReleaseFundsRequest request,
        CancellationToken ct = default)
    {
        return await PostAsync<ActionResponse>("/flows/release-funds", request, ct);
    }

    public async Task<EditProjectProfileResponse> EditProjectProfileAsync(
        EditProjectProfileRequest request,
        CancellationToken ct = default)
    {
        return await PostAsync<EditProjectProfileResponse>("/flows/edit-project-profile", request, ct);
    }

    public async Task<FetchProjectProfileResponse> FetchProjectProfileAsync(
        FetchProjectProfileRequest request,
        CancellationToken ct = default)
    {
        return await PostAsync<FetchProjectProfileResponse>("/flows/fetch-project-profile", request, ct);
    }

    public async Task<UploadToBlossomResponse> UploadToBlossomAsync(
        UploadToBlossomRequest request,
        CancellationToken ct = default)
    {
        return await PostAsync<UploadToBlossomResponse>("/flows/upload-to-blossom", request, ct);
    }

    // ═══════════════════════════════════════════════════════════════════
    // HTTP Helpers
    // ═══════════════════════════════════════════════════════════════════

    private async Task<T> GetAsync<T>(string path, CancellationToken ct) where T : new()
    {
        var response = await http.GetAsync($"{baseUrl}{path}", ct);
        var body = await response.Content.ReadAsStringAsync(ct);
        return JsonSerializer.Deserialize<T>(body, JsonOptions) ?? new T();
    }

    private async Task<T> PostAsync<T>(string path, object? payload, CancellationToken ct) where T : new()
    {
        HttpContent? content = null;
        if (payload != null)
        {
            var json = JsonSerializer.Serialize(payload, JsonOptions);
            content = new StringContent(json, Encoding.UTF8, "application/json");
        }

        var response = await http.PostAsync($"{baseUrl}{path}", content, ct);
        var body = await response.Content.ReadAsStringAsync(ct);
        return JsonSerializer.Deserialize<T>(body, JsonOptions) ?? new T();
    }

    public void Dispose()
    {
        http.Dispose();
    }
}
