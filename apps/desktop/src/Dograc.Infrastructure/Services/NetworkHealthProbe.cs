using System.Net.Sockets;
using Dograc.Core.Services;

namespace Dograc.Infrastructure.Services;

/// <summary>
/// Performs lightweight HTTP and TCP probes for local infrastructure health checks.
/// </summary>
public sealed class NetworkHealthProbe : IHealthProbe
{
    private readonly IHttpClientFactory _httpClientFactory;

    public NetworkHealthProbe(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    public async Task<bool> IsUrlAccessibleAsync(string url, TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        try
        {
            using var client = _httpClientFactory.CreateClient("HealthProbe");
            client.Timeout = timeout;
            using var response = await client.GetAsync(url, cancellationToken);
            var code = (int)response.StatusCode;
            return code is >= 200 and < 400;
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> IsPortOpenAsync(string host, int port, TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        try
        {
            using var tcpClient = new TcpClient();
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(timeout);
            await tcpClient.ConnectAsync(host, port, timeoutCts.Token);
            return tcpClient.Connected;
        }
        catch
        {
            return false;
        }
    }
}
