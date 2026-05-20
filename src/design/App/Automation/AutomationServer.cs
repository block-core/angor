using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using App.UI.Shell;
using Microsoft.Extensions.DependencyInjection;
using static App.Automation.AutomationDtos;
using static App.Automation.AutomationFlowDtos;

namespace App.Automation;

/// <summary>
/// Lightweight HTTP server over TcpListener for test automation.
/// Activated by ANGOR_TEST_API=1 environment variable.
/// Port configured via ANGOR_TEST_API_PORT environment variable.
///
/// All visual-tree and ViewModel operations are dispatched on Dispatcher.UIThread.
/// </summary>
public sealed class AutomationServer : IDisposable
{
    private readonly TcpListener listener;
    private readonly IServiceProvider services;
    private readonly CancellationTokenSource cts = new();
    private readonly int port;
    private Task? listenTask;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = false,
    };

    /// <summary>Default port for Android automation (env vars are not available on Android).</summary>
    private const int AndroidDefaultPort = 18721;

    private AutomationServer(int port, IServiceProvider services, bool bindAny = false)
    {
        this.port = port;
        this.services = services;
        listener = new TcpListener(bindAny ? IPAddress.Any : IPAddress.Loopback, port);
    }

    /// <summary>
    /// Starts the automation server if ANGOR_TEST_API=1 is set (desktop)
    /// or always on Android Debug builds (fixed port, bind to all interfaces).
    /// Call after DI container is built.
    /// </summary>
    public static AutomationServer? StartIfEnabled(IServiceProvider services)
    {
        // On Android, always start with a fixed port (env vars are not available).
        // Bind to IPAddress.Any so adb forward/reverse can reach the server.
        if (OperatingSystem.IsAndroid())
        {
            var server = new AutomationServer(AndroidDefaultPort, services, bindAny: true);
            server.Start();
            return server;
        }

        var enabled = Environment.GetEnvironmentVariable("ANGOR_TEST_API");
        if (!string.Equals(enabled, "1", StringComparison.Ordinal))
        {
            return null;
        }

        var portStr = Environment.GetEnvironmentVariable("ANGOR_TEST_API_PORT");
        if (!int.TryParse(portStr, out var port) || port <= 0)
        {
            Console.WriteLine("[AutomationServer] ANGOR_TEST_API_PORT not set or invalid. Server not started.");
            return null;
        }

        var desktopServer = new AutomationServer(port, services);
        desktopServer.Start();
        return desktopServer;
    }

    private void Start()
    {
        listener.Start();
        Console.WriteLine($"[AutomationServer] Listening on http://127.0.0.1:{port}");
        listenTask = Task.Run(() => AcceptLoop(cts.Token));
    }

    private async Task AcceptLoop(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                var client = await listener.AcceptTcpClientAsync();
                // Fire-and-forget each connection
                _ = Task.Run(() => HandleConnection(client, ct), ct);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (ObjectDisposedException)
            {
                break;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[AutomationServer] Accept error: {ex.Message}");
            }
        }
    }

    private async Task HandleConnection(TcpClient client, CancellationToken ct)
    {
        try
        {
            using (client)
            {
                client.ReceiveTimeout = 30_000;
                client.SendTimeout = 30_000;

                using var stream = client.GetStream();
                using var reader = new StreamReader(stream, Encoding.UTF8, leaveOpen: true);

                // Read request line: "GET /path HTTP/1.1"
                var requestLine = await reader.ReadLineAsync();
                if (string.IsNullOrWhiteSpace(requestLine))
                {
                    return;
                }

                var parts = requestLine.Split(' ', 3);
                if (parts.Length < 2)
                {
                    return;
                }

                var method = parts[0].ToUpperInvariant();
                var path = parts[1];

                // Read headers
                var contentLength = 0;
                while (true)
                {
                    var headerLine = await reader.ReadLineAsync();
                    if (string.IsNullOrEmpty(headerLine))
                    {
                        break;
                    }

                    if (headerLine.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase))
                    {
                        int.TryParse(headerLine.AsSpan(15).Trim(stackalloc char[] { ' ' }), out contentLength);
                    }
                }

                // Read body
                string? body = null;
                if (contentLength > 0)
                {
                    var buffer = new char[contentLength];
                    var totalRead = 0;
                    while (totalRead < contentLength)
                    {
                        var read = await reader.ReadAsync(buffer, totalRead, contentLength - totalRead);
                        if (read == 0) break;
                        totalRead += read;
                    }

                    body = new string(buffer, 0, totalRead);
                }

                // Route
                var (statusCode, responseBody) = await RouteRequest(method, path, body);

                // Write response
                var responseJson = JsonSerializer.Serialize(responseBody, responseBody.GetType(), JsonOptions);
                var responseBytes = Encoding.UTF8.GetBytes(responseJson);
                var header = $"HTTP/1.1 {statusCode} OK\r\nContent-Type: application/json\r\nContent-Length: {responseBytes.Length}\r\nConnection: close\r\n\r\n";
                var headerBytes = Encoding.UTF8.GetBytes(header);

                await stream.WriteAsync(headerBytes, 0, headerBytes.Length, ct);
                await stream.WriteAsync(responseBytes, 0, responseBytes.Length, ct);
                await stream.FlushAsync(ct);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[AutomationServer] Connection error: {ex.Message}");
        }
    }

    private async Task<(int statusCode, object body)> RouteRequest(string method, string path, string? body)
    {
        try
        {
            // GET /health
            if (method == "GET" && path == "/health")
            {
                return (200, new HealthResponse { Ready = true });
            }

            // GET /control/{automationId}
            if (method == "GET" && path.StartsWith("/control/", StringComparison.Ordinal))
            {
                var automationId = Uri.UnescapeDataString(path.Substring("/control/".Length));
                var info = await Dispatcher.UIThread.InvokeAsync(() => FindControl(automationId));
                return (200, info);
            }

            // POST /click
            if (method == "POST" && path == "/click")
            {
                var req = Deserialize<ClickRequest>(body);
                var result = await Dispatcher.UIThread.InvokeAsync(() => ClickControl(req));
                return (200, result);
            }

            // POST /navigate
            if (method == "POST" && path == "/navigate")
            {
                var req = Deserialize<NavigateRequest>(body);
                var result = await Dispatcher.UIThread.InvokeAsync(() => Navigate(req.Section));
                return (200, result);
            }

            // POST /type-text
            if (method == "POST" && path == "/type-text")
            {
                var req = Deserialize<TypeTextRequest>(body);
                var result = await Dispatcher.UIThread.InvokeAsync(() => TypeText(req));
                return (200, result);
            }

            // GET /vm/{typeName}/{property}
            if (method == "GET" && path.StartsWith("/vm/", StringComparison.Ordinal))
            {
                var vmPath = path.Substring("/vm/".Length);
                var slashIdx = vmPath.IndexOf('/');
                if (slashIdx < 0)
                {
                    return (400, new ActionResponse { Success = false, Error = "Expected /vm/{type}/{property}" });
                }

                var typeName = Uri.UnescapeDataString(vmPath.Substring(0, slashIdx));
                var property = Uri.UnescapeDataString(vmPath.Substring(slashIdx + 1));
                var result = await Dispatcher.UIThread.InvokeAsync(() => GetVmProperty(typeName, property));
                return (200, result);
            }

            // POST /vm/invoke
            if (method == "POST" && path == "/vm/invoke")
            {
                var req = Deserialize<VmInvokeRequest>(body);
                var result = await InvokeVmMethod(req);
                return (200, result);
            }

            // POST /vm/set-property
            if (method == "POST" && path == "/vm/set-property")
            {
                var req = Deserialize<SetVmPropertyRequest>(body);
                var result = await Dispatcher.UIThread.InvokeAsync(() => SetVmProperty(req));
                return (200, result);
            }

            // POST /wipe
            if (method == "POST" && path == "/wipe")
            {
                var result = await Dispatcher.UIThread.InvokeAsync(() => WipeData());
                return (200, result);
            }

            // POST /debug-mode
            if (method == "POST" && path == "/debug-mode")
            {
                var result = await Dispatcher.UIThread.InvokeAsync(() => EnableDebugMode());
                return (200, result);
            }

            // POST /wait-for-control
            if (method == "POST" && path == "/wait-for-control")
            {
                var req = Deserialize<WaitForControlRequest>(body);
                var info = await WaitForControl(req);
                return (200, info);
            }

            // POST /flows/create-wallet-and-fund
            if (method == "POST" && path == "/flows/create-wallet-and-fund")
            {
                var req = Deserialize<CreateWalletAndFundRequest>(body);
                var result = await AutomationFlows.CreateWalletAndFundAsync(services, req);
                return (200, result);
            }

            // POST /flows/create-fund-project
            if (method == "POST" && path == "/flows/create-fund-project")
            {
                var req = Deserialize<CreateFundProjectRequest>(body);
                var result = await AutomationFlows.CreateFundProjectAsync(services, req);
                return (200, result);
            }

            // POST /flows/create-invest-project
            if (method == "POST" && path == "/flows/create-invest-project")
            {
                var req = Deserialize<CreateInvestProjectRequest>(body);
                var result = await AutomationFlows.CreateInvestProjectAsync(services, req);
                return (200, result);
            }

            // POST /flows/invest
            if (method == "POST" && path == "/flows/invest")
            {
                var req = Deserialize<InvestInProjectRequest>(body);
                var result = await AutomationFlows.InvestInProjectAsync(services, req);
                return (200, result);
            }

            // POST /flows/approve-investments
            if (method == "POST" && path == "/flows/approve-investments")
            {
                var req = Deserialize<ApproveInvestmentsRequest>(body);
                var result = await AutomationFlows.ApproveInvestmentsAsync(services, req);
                return (200, result);
            }

            // POST /flows/confirm-investment
            if (method == "POST" && path == "/flows/confirm-investment")
            {
                var req = Deserialize<ConfirmInvestmentRequest>(body);
                var result = await AutomationFlows.ConfirmInvestmentAsync(services, req);
                return (200, result);
            }

            // POST /flows/claim-stage
            if (method == "POST" && path == "/flows/claim-stage")
            {
                var req = Deserialize<ClaimStageRequest>(body);
                var result = await AutomationFlows.ClaimStageAsync(services, req);
                return (200, result);
            }

            // POST /flows/recovery
            if (method == "POST" && path == "/flows/recovery")
            {
                var req = Deserialize<RecoveryRequest>(body);
                var result = await AutomationFlows.ExecuteRecoveryAsync(services, req);
                return (200, result);
            }

            // POST /flows/release-funds
            if (method == "POST" && path == "/flows/release-funds")
            {
                var req = Deserialize<ReleaseFundsRequest>(body);
                var result = await AutomationFlows.ReleaseFundsToInvestorsAsync(services, req);
                return (200, result);
            }

            // POST /flows/edit-project-profile
            if (method == "POST" && path == "/flows/edit-project-profile")
            {
                var req = Deserialize<EditProjectProfileRequest>(body);
                var result = await AutomationFlows.EditProjectProfileAsync(services, req);
                return (200, result);
            }

            // POST /flows/fetch-project-profile
            if (method == "POST" && path == "/flows/fetch-project-profile")
            {
                var req = Deserialize<FetchProjectProfileRequest>(body);
                var result = await AutomationFlows.FetchProjectProfileAsync(services, req);
                return (200, result);
            }

            // POST /flows/upload-to-blossom
            if (method == "POST" && path == "/flows/upload-to-blossom")
            {
                var req = Deserialize<UploadToBlossomRequest>(body);
                var result = await AutomationFlows.UploadToBlossomAsync(services, req);
                return (200, result);
            }

            return (404, new ActionResponse { Success = false, Error = $"Unknown route: {method} {path}" });
        }
        catch (Exception ex)
        {
            return (500, new ActionResponse { Success = false, Error = ex.ToString() });
        }
    }

    // ═══════════════════════════════════════════════════════════════════
    // Handlers (all run on UI thread unless otherwise noted)
    // ═══════════════════════════════════════════════════════════════════

    private ControlInfo FindControl(string automationId)
    {
        var window = GetMainWindow();
        if (window == null)
        {
            return new ControlInfo { Found = false };
        }

        var control = FindByAutomationId(window, automationId);
        if (control == null)
        {
            // Also try by x:Name
            control = FindByName(window, automationId);
        }

        if (control == null)
        {
            return new ControlInfo { Found = false, AutomationId = automationId };
        }

        return BuildControlInfo(control, automationId);
    }

    private ActionResponse ClickControl(ClickRequest req)
    {
        var window = GetMainWindow();
        if (window == null)
        {
            return new ActionResponse { Success = false, Error = "No main window" };
        }

        var id = req.AutomationId ?? req.Name;
        if (string.IsNullOrEmpty(id))
        {
            return new ActionResponse { Success = false, Error = "automationId or name required" };
        }

        var control = FindByAutomationId(window, id) ?? FindByName(window, id);
        if (control is not Button button)
        {
            return new ActionResponse { Success = false, Error = $"Button '{id}' not found" };
        }

        button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent, button));
        Dispatcher.UIThread.RunJobs();
        return new ActionResponse { Success = true };
    }

    private ActionResponse Navigate(string section)
    {
        var window = GetMainWindow();
        if (window == null)
        {
            return new ActionResponse { Success = false, Error = "No main window" };
        }

        var shellView = window.GetVisualDescendants()
            .OfType<ShellView>()
            .FirstOrDefault();

        if (shellView?.DataContext is not ShellViewModel vm)
        {
            return new ActionResponse { Success = false, Error = "ShellViewModel not found" };
        }

        if (string.Equals(section, "Settings", StringComparison.OrdinalIgnoreCase))
        {
            vm.NavigateToSettings();
            Dispatcher.UIThread.RunJobs();
            return new ActionResponse { Success = true };
        }

        var navItem = vm.NavEntries.OfType<NavItem>().FirstOrDefault(n =>
            string.Equals(n.Label, section, StringComparison.OrdinalIgnoreCase));

        if (navItem == null)
        {
            return new ActionResponse { Success = false, Error = $"Section '{section}' not found in NavEntries" };
        }

        vm.SelectedNavItem = navItem;
        Dispatcher.UIThread.RunJobs();
        return new ActionResponse { Success = true };
    }

    private ActionResponse TypeText(TypeTextRequest req)
    {
        var window = GetMainWindow();
        if (window == null)
        {
            return new ActionResponse { Success = false, Error = "No main window" };
        }

        var control = FindByAutomationId(window, req.AutomationId) ?? FindByName(window, req.AutomationId);
        if (control is TextBox textBox)
        {
            textBox.Text = req.Text;
            Dispatcher.UIThread.RunJobs();
            return new ActionResponse { Success = true };
        }

        return new ActionResponse { Success = false, Error = $"TextBox '{req.AutomationId}' not found" };
    }

    private ValueResponse GetVmProperty(string typeName, string property)
    {
        var vm = ResolveVmByName(typeName);
        if (vm == null)
        {
            return new ValueResponse { Error = $"ViewModel '{typeName}' not found in DI" };
        }

        try
        {
            var value = GetNestedProperty(vm, property);
            return new ValueResponse { Value = value };
        }
        catch (Exception ex)
        {
            return new ValueResponse { Error = ex.Message };
        }
    }

    private async Task<ActionResponse> InvokeVmMethod(VmInvokeRequest req)
    {
        var vm = await Dispatcher.UIThread.InvokeAsync(() => ResolveVmByName(req.Type));
        if (vm == null)
        {
            return new ActionResponse { Success = false, Error = $"ViewModel '{req.Type}' not found in DI" };
        }

        try
        {
            var methodInfo = vm.GetType().GetMethod(req.Method,
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);

            if (methodInfo == null)
            {
                return new ActionResponse { Success = false, Error = $"Method '{req.Method}' not found on {vm.GetType().Name}" };
            }

            object? result;
            if (methodInfo.ReturnType == typeof(Task) || (methodInfo.ReturnType.IsGenericType && methodInfo.ReturnType.GetGenericTypeDefinition() == typeof(Task<>)))
            {
                // Async method — invoke on UI thread, then await
                Task task = await Dispatcher.UIThread.InvokeAsync<Task>(() => (Task)methodInfo.Invoke(vm, req.Args)!);
                await task;
                result = null;

                // If Task<T>, extract result
                if (methodInfo.ReturnType.IsGenericType)
                {
                    result = methodInfo.ReturnType.GetProperty("Result")?.GetValue(task);
                }
            }
            else
            {
                result = await Dispatcher.UIThread.InvokeAsync(() => methodInfo.Invoke(vm, req.Args));
            }

            return new ActionResponse { Success = true };
        }
        catch (Exception ex)
        {
            return new ActionResponse { Success = false, Error = ex.InnerException?.Message ?? ex.Message };
        }
    }

    private ActionResponse SetVmProperty(SetVmPropertyRequest req)
    {
        var vm = ResolveVmByName(req.Type);
        if (vm == null)
        {
            return new ActionResponse { Success = false, Error = $"ViewModel '{req.Type}' not found in DI" };
        }

        try
        {
            var prop = vm.GetType().GetProperty(req.Property, BindingFlags.Public | BindingFlags.Instance);
            if (prop == null)
            {
                return new ActionResponse { Success = false, Error = $"Property '{req.Property}' not found on {vm.GetType().Name}" };
            }

            if (!prop.CanWrite)
            {
                return new ActionResponse { Success = false, Error = $"Property '{req.Property}' is read-only" };
            }

            var value = ConvertValue(req.Value, prop.PropertyType);
            prop.SetValue(vm, value);
            Dispatcher.UIThread.RunJobs();
            return new ActionResponse { Success = true };
        }
        catch (Exception ex)
        {
            return new ActionResponse { Success = false, Error = ex.Message };
        }
    }

    private ActionResponse WipeData()
    {
        var window = GetMainWindow();
        if (window == null)
        {
            return new ActionResponse { Success = false, Error = "No main window" };
        }

        var shellView = window.GetVisualDescendants().OfType<ShellView>().FirstOrDefault();
        if (shellView?.DataContext is not ShellViewModel shellVm)
        {
            return new ActionResponse { Success = false, Error = "ShellViewModel not found" };
        }

        shellVm.NavigateToSettings();
        Dispatcher.UIThread.RunJobs();

        var settingsVm = services.GetService<UI.Sections.Settings.SettingsViewModel>();
        if (settingsVm == null)
        {
            return new ActionResponse { Success = false, Error = "SettingsViewModel not found" };
        }

        settingsVm.ConfirmWipeData();
        Dispatcher.UIThread.RunJobs();
        return new ActionResponse { Success = true };
    }

    private ActionResponse EnableDebugMode()
    {
        var settingsVm = services.GetService<UI.Sections.Settings.SettingsViewModel>();
        if (settingsVm == null)
        {
            return new ActionResponse { Success = false, Error = "SettingsViewModel not found" };
        }

        settingsVm.IsDebugMode = true;
        Dispatcher.UIThread.RunJobs();
        return new ActionResponse { Success = true };
    }

    private async Task<ControlInfo> WaitForControl(WaitForControlRequest req)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromMilliseconds(req.TimeoutMs);

        while (DateTime.UtcNow < deadline)
        {
            var info = await Dispatcher.UIThread.InvokeAsync(() => FindControl(req.AutomationId));
            if (info.Found && (!req.Visible || info.IsVisible))
            {
                return info;
            }

            await Task.Delay(100);
        }

        return new ControlInfo { Found = false, AutomationId = req.AutomationId };
    }

    // ═══════════════════════════════════════════════════════════════════
    // Helpers
    // ═══════════════════════════════════════════════════════════════════

    private Window? GetMainWindow()
    {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            return desktop.MainWindow;
        }

        return null;
    }

    private static Visual? FindByAutomationId(Visual root, string automationId)
    {
        return root.GetVisualDescendants()
            .OfType<Visual>()
            .FirstOrDefault(c => AutomationProperties.GetAutomationId(c) == automationId);
    }

    private static Control? FindByName(Visual root, string name)
    {
        return root.GetVisualDescendants()
            .OfType<Control>()
            .FirstOrDefault(c => c.Name == name);
    }

    private static ControlInfo BuildControlInfo(Visual control, string automationId)
    {
        string? text = null;
        if (control is TextBlock tb)
        {
            text = tb.Text;
        }
        else if (control is TextBox tx)
        {
            text = tx.Text;
        }
        else if (control is ContentControl cc && cc.Content is string s)
        {
            text = s;
        }

        var isEnabled = control is Control c2 && c2.IsEnabled;

        return new ControlInfo
        {
            Found = true,
            AutomationId = automationId,
            Name = (control as Control)?.Name,
            Type = control.GetType().Name,
            IsVisible = control.IsVisible,
            IsEnabled = isEnabled,
            Text = text,
            DataContextType = (control as StyledElement)?.DataContext?.GetType().Name,
        };
    }

    private object? ResolveVmByName(string typeName)
    {
        // Try common ViewModel types by short name
        var assembly = typeof(ShellViewModel).Assembly;
        var type = assembly.GetTypes().FirstOrDefault(t =>
            string.Equals(t.Name, typeName, StringComparison.OrdinalIgnoreCase));

        if (type == null)
        {
            return null;
        }

        return services.GetService(type);
    }

    private static object? GetNestedProperty(object obj, string propertyPath)
    {
        var current = obj;
        foreach (var segment in propertyPath.Split('.'))
        {
            if (current == null) return null;

            // Handle indexer: "Investments[0]"
            var bracketIdx = segment.IndexOf('[');
            if (bracketIdx >= 0)
            {
                var propName = segment.Substring(0, bracketIdx);
                var indexStr = segment.Substring(bracketIdx + 1, segment.Length - bracketIdx - 2);

                var prop = current.GetType().GetProperty(propName, BindingFlags.Public | BindingFlags.Instance);
                current = prop?.GetValue(current);

                if (current != null && int.TryParse(indexStr, out var index))
                {
                    if (current is System.Collections.IList list && index < list.Count)
                    {
                        current = list[index];
                    }
                    else
                    {
                        return null;
                    }
                }
            }
            else
            {
                var prop = current.GetType().GetProperty(segment, BindingFlags.Public | BindingFlags.Instance);
                current = prop?.GetValue(current);
            }
        }

        return current;
    }

    private static T Deserialize<T>(string? body) where T : new()
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return new T();
        }

        return JsonSerializer.Deserialize<T>(body, JsonOptions) ?? new T();
    }

    private static object? ConvertValue(object? rawValue, Type targetType)
    {
        if (rawValue == null)
        {
            return targetType.IsValueType ? Activator.CreateInstance(targetType) : null;
        }

        // Handle JsonElement from System.Text.Json deserialization
        if (rawValue is JsonElement jsonElement)
        {
            if (targetType == typeof(string))
                return jsonElement.GetString();
            if (targetType == typeof(int))
                return jsonElement.GetInt32();
            if (targetType == typeof(long))
                return jsonElement.GetInt64();
            if (targetType == typeof(double))
                return jsonElement.GetDouble();
            if (targetType == typeof(decimal))
                return jsonElement.GetDecimal();
            if (targetType == typeof(bool))
                return jsonElement.GetBoolean();
            if (targetType == typeof(DateTime))
                return jsonElement.GetDateTime();
            if (targetType == typeof(DateTime?))
                return jsonElement.ValueKind == JsonValueKind.Null ? null : jsonElement.GetDateTime();

            return jsonElement.Deserialize(targetType, JsonOptions);
        }

        return Convert.ChangeType(rawValue, targetType);
    }

    public void Dispose()
    {
        cts.Cancel();
        listener.Stop();
        cts.Dispose();
    }
}
