using System.Collections.Concurrent;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace Angor.Shared.Services.Indexer.Electrum;

/// <summary>
/// Electrum JSON-RPC client over SSL/TCP.
/// Implements persistent connections with request/response correlation.
/// </summary>
public class ElectrumClient : IDisposable
{
    private readonly ILogger<ElectrumClient> _logger;
    private readonly ElectrumServerConfig _config;
    private readonly SemaphoreSlim _connectionLock = new(1, 1);
    private readonly ConcurrentDictionary<int, TaskCompletionSource<JsonElement>> _pendingRequests = new();
    private readonly CancellationTokenSource _readLoopCts = new();

    private TcpClient? _tcpClient;
    private SslStream? _sslStream;
    private StreamReader? _reader;
    private StreamWriter? _writer;
    private Task? _readLoopTask;
    private int _requestId;
    private bool _disposed;
    private bool _isConnected;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public bool IsConnected => _isConnected && _tcpClient?.Connected == true;
    public string ServerUrl => $"{_config.Host}:{_config.Port}";

    public ElectrumClient(ILogger<ElectrumClient> logger, ElectrumServerConfig config)
    {
        _logger = logger;
        _config = config;
    }

    public async Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        await _connectionLock.WaitAsync(cancellationToken);
        try
        {
            if (IsConnected)
                return;

            _logger.LogInformation("Connecting to Electrum server {Host}:{Port}", _config.Host, _config.Port);

            _tcpClient = new TcpClient
            {
                ReceiveTimeout = (int)_config.Timeout.TotalMilliseconds,
                SendTimeout = (int)_config.Timeout.TotalMilliseconds
            };

            await _tcpClient.ConnectAsync(_config.Host, _config.Port, cancellationToken);

            if (_config.UseSsl)
            {
                _sslStream = new SslStream(
                            _tcpClient.GetStream(),
                           false,
                          ValidateServerCertificate,
                    null);

                await _sslStream.AuthenticateAsClientAsync(
                        new SslClientAuthenticationOptions
                        {
                            TargetHost = _config.Host,
                            EnabledSslProtocols = System.Security.Authentication.SslProtocols.Tls12 | System.Security.Authentication.SslProtocols.Tls13
                        },
                           cancellationToken);

                _reader = new StreamReader(_sslStream, Encoding.UTF8);
                _writer = new StreamWriter(_sslStream, Encoding.UTF8) { AutoFlush = true };
            }
            else
            {
                var stream = _tcpClient.GetStream();
                _reader = new StreamReader(stream, Encoding.UTF8);
                _writer = new StreamWriter(stream, Encoding.UTF8) { AutoFlush = true };
            }

            _isConnected = true;

            // Start background read loop
            _readLoopTask = Task.Run(() => ReadLoopAsync(_readLoopCts.Token), _readLoopCts.Token);

            // Negotiate protocol version
            await NegotiateProtocolVersionAsync(cancellationToken);

            _logger.LogInformation("Connected to Electrum server {Host}:{Port}", _config.Host, _config.Port);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to connect to Electrum server {Host}:{Port}", _config.Host, _config.Port);
            await DisconnectAsync();
            throw;
        }
        finally
        {
            _connectionLock.Release();
        }
    }

    private bool ValidateServerCertificate(object sender, X509Certificate? certificate, X509Chain? chain, SslPolicyErrors sslPolicyErrors)
    {
        if (_config.AllowSelfSignedCertificates)
            return true;

        if (sslPolicyErrors == SslPolicyErrors.None)
            return true;

        _logger.LogWarning("SSL certificate validation error: {Errors}", sslPolicyErrors);
        return false;
    }

    private async Task NegotiateProtocolVersionAsync(CancellationToken cancellationToken)
    {
        var result = await SendRequestAsync<JsonElement[]>(
    "server.version",
       new object[] { "Angor/1.0", "1.4" },
   cancellationToken);

        if (result is { Length: >= 2 })
        {
            _logger.LogDebug("Electrum protocol version: {Version}, Server: {Server}",
      result[1].GetString(), result[0].GetString());
        }
    }

    public async Task DisconnectAsync()
    {
        await _connectionLock.WaitAsync();
        try
        {
            _isConnected = false;

            _readLoopCts.Cancel();

            if (_readLoopTask != null)
            {
                try
                {
                    await _readLoopTask.WaitAsync(TimeSpan.FromSeconds(2));
                }
                catch (TimeoutException) { }
                catch (OperationCanceledException) { }
            }

            _writer?.Dispose();
            _reader?.Dispose();
            _sslStream?.Dispose();
            _tcpClient?.Dispose();

            _writer = null;
            _reader = null;
            _sslStream = null;
            _tcpClient = null;

            // Complete any pending requests with cancellation
            foreach (var pending in _pendingRequests)
            {
                pending.Value.TrySetCanceled();
            }
            _pendingRequests.Clear();

            _logger.LogInformation("Disconnected from Electrum server");
        }
        finally
        {
            _connectionLock.Release();
        }
    }

    private async Task ReadLoopAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested && _reader != null)
            {
                var line = await _reader.ReadLineAsync(cancellationToken);
                if (line == null)
                {
                    _logger.LogWarning("Electrum server closed connection");
                    _isConnected = false;
                    break;
                }

                try
                {
                    ProcessResponse(line);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing Electrum response: {Line}", line);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Expected during shutdown
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in Electrum read loop");
            _isConnected = false;
        }
    }

    private void ProcessResponse(string line)
    {
        using var doc = JsonDocument.Parse(line);
        var root = doc.RootElement;

        if (root.TryGetProperty("id", out var idElement))
        {
            var id = idElement.GetInt32();
            if (_pendingRequests.TryRemove(id, out var tcs))
            {
                if (root.TryGetProperty("error", out var error) && error.ValueKind != JsonValueKind.Null)
                {
                    var errorMessage = error.TryGetProperty("message", out var msg)
             ? msg.GetString()
                 : error.ToString();
                    tcs.SetException(new ElectrumException(errorMessage ?? "Unknown error"));
                }
                else if (root.TryGetProperty("result", out var result))
                {
                    tcs.SetResult(result.Clone());
                }
                else
                {
                    tcs.SetException(new ElectrumException("Invalid response format"));
                }
            }
        }
        else
        {
            // Server notification (no id)
            if (root.TryGetProperty("method", out var method))
            {
                _logger.LogDebug("Received notification: {Method}", method.GetString());
            }
        }
    }

    public async Task<T> SendRequestAsync<T>(string method, object[]? parameters = null, CancellationToken cancellationToken = default)
    {
        var result = await SendRequestInternalAsync(method, parameters, cancellationToken);
        return JsonSerializer.Deserialize<T>(result.GetRawText(), JsonOptions)!;
    }

    public async Task<JsonElement> SendRequestAsync(string method, object[]? parameters = null, CancellationToken cancellationToken = default)
    {
        return await SendRequestInternalAsync(method, parameters, cancellationToken);
    }

    private async Task<JsonElement> SendRequestInternalAsync(string method, object[]? parameters, CancellationToken cancellationToken)
    {
        if (!IsConnected)
        {
            await ConnectAsync(cancellationToken);
        }

        var id = Interlocked.Increment(ref _requestId);
        var request = new ElectrumRequest
        {
            Id = id,
            Method = method,
            Params = parameters ?? Array.Empty<object>()
        };

        var tcs = new TaskCompletionSource<JsonElement>(TaskCreationOptions.RunContinuationsAsynchronously);
        _pendingRequests[id] = tcs;

        try
        {
            var json = JsonSerializer.Serialize(request, JsonOptions);
            _logger.LogTrace("Sending Electrum request: {Request}", json);

            if (_writer == null)
                throw new ElectrumException("Not connected to server");

            await _writer.WriteLineAsync(json);

            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(_config.Timeout);

            try
            {
                return await tcs.Task.WaitAsync(timeoutCts.Token);
            }
            catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
            {
                throw new TimeoutException($"Electrum request timed out: {method}");
            }
        }
        catch
        {
            _pendingRequests.TryRemove(id, out _);
            throw;
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _readLoopCts.Cancel();
        _readLoopCts.Dispose();
        _connectionLock.Dispose();
        _writer?.Dispose();
        _reader?.Dispose();
        _sslStream?.Dispose();
        _tcpClient?.Dispose();

        GC.SuppressFinalize(this);
    }
}

/// <summary>
/// Electrum JSON-RPC request format.
/// </summary>
internal class ElectrumRequest
{
    [JsonPropertyName("jsonrpc")]
    public string JsonRpc { get; } = "2.0";

    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("method")]
    public string Method { get; set; } = string.Empty;

    [JsonPropertyName("params")]
    public object[] Params { get; set; } = Array.Empty<object>();
}

/// <summary>
/// Exception thrown for Electrum protocol errors.
/// </summary>
public class ElectrumException : Exception
{
    public ElectrumException(string message) : base(message) { }
    public ElectrumException(string message, Exception innerException) : base(message, innerException) { }
}

/// <summary>
/// Configuration for an Electrum server connection.
/// </summary>
public class ElectrumServerConfig
{
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 50002;
    public bool UseSsl { get; set; } = true;
    public bool AllowSelfSignedCertificates { get; set; } = true;
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(30);
    public string Network { get; set; } = "mainnet";
}
