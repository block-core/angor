using System.Text.Json.Serialization;

namespace App.Automation;

/// <summary>
/// JSON DTOs for the test automation HTTP protocol.
/// </summary>
public static class AutomationDtos
{
    public sealed class HealthResponse
    {
        [JsonPropertyName("ready")]
        public bool Ready { get; init; }
    }

    public sealed class ControlInfo
    {
        [JsonPropertyName("automationId")]
        public string? AutomationId { get; init; }

        [JsonPropertyName("name")]
        public string? Name { get; init; }

        [JsonPropertyName("type")]
        public string? Type { get; init; }

        [JsonPropertyName("isVisible")]
        public bool IsVisible { get; init; }

        [JsonPropertyName("isEnabled")]
        public bool IsEnabled { get; init; }

        [JsonPropertyName("text")]
        public string? Text { get; init; }

        [JsonPropertyName("dataContextType")]
        public string? DataContextType { get; init; }

        [JsonPropertyName("found")]
        public bool Found { get; init; }
    }

    public sealed class ClickRequest
    {
        [JsonPropertyName("automationId")]
        public string AutomationId { get; init; } = "";

        [JsonPropertyName("name")]
        public string? Name { get; init; }
    }

    public sealed class NavigateRequest
    {
        [JsonPropertyName("section")]
        public string Section { get; init; } = "";
    }

    public sealed class VmInvokeRequest
    {
        [JsonPropertyName("type")]
        public string Type { get; init; } = "";

        [JsonPropertyName("method")]
        public string Method { get; init; } = "";

        [JsonPropertyName("args")]
        public object[]? Args { get; init; }
    }

    public sealed class TypeTextRequest
    {
        [JsonPropertyName("automationId")]
        public string AutomationId { get; init; } = "";

        [JsonPropertyName("text")]
        public string Text { get; init; } = "";
    }

    public sealed class SetVmPropertyRequest
    {
        [JsonPropertyName("type")]
        public string Type { get; init; } = "";

        [JsonPropertyName("property")]
        public string Property { get; init; } = "";

        [JsonPropertyName("value")]
        public object? Value { get; init; }

        [JsonPropertyName("valueType")]
        public string? ValueType { get; init; }
    }

    public sealed class WaitForControlRequest
    {
        [JsonPropertyName("automationId")]
        public string AutomationId { get; init; } = "";

        [JsonPropertyName("timeoutMs")]
        public int TimeoutMs { get; init; } = 10000;

        [JsonPropertyName("visible")]
        public bool Visible { get; init; } = true;
    }

    public sealed class WipeDataRequest
    {
        [JsonPropertyName("deleteRecoveryWalletFiles")]
        public bool DeleteRecoveryWalletFiles { get; init; }
    }

    public sealed class SwitchNetworkRequest
    {
        [JsonPropertyName("network")]
        public string Network { get; init; } = "";
    }

    public sealed class ActionResponse
    {
        [JsonPropertyName("success")]
        public bool Success { get; init; }

        [JsonPropertyName("error")]
        public string? Error { get; init; }
    }

    public sealed class ValueResponse
    {
        [JsonPropertyName("value")]
        public object? Value { get; init; }

        [JsonPropertyName("error")]
        public string? Error { get; init; }
    }
}
