using System.Net;
using System.Net.Sockets;
using AgrusScanner.Models;

namespace AgrusScanner.Services;

public class PortScanner
{
    public async Task<List<PortResult>> ScanAsync(
        IPAddress address,
        int[] ports,
        int timeoutMs,
        int maxConcurrency,
        CancellationToken ct)
    {
        var results = new List<PortResult>();
        using var semaphore = new SemaphoreSlim(maxConcurrency);
        var lockObj = new object();

        var tasks = ports.Select(async port =>
        {
            await semaphore.WaitAsync(ct);
            try
            {
                var isOpen = await IsPortOpenAsync(address, port, timeoutMs, ct);
                if (isOpen)
                {
                    var result = new PortResult
                    {
                        Port = port,
                        ServiceName = ServiceNameMap.GetServiceName(port),
                        IsOpen = true
                    };
                    lock (lockObj) { results.Add(result); }
                }
            }
            finally
            {
                semaphore.Release();
            }
        });

        await Task.WhenAll(tasks);
        return results.OrderBy(r => r.Port).ToList();
    }

    private static async Task<bool> IsPortOpenAsync(
        IPAddress address, int port, int timeoutMs, CancellationToken ct)
    {
        try
        {
            using var client = new TcpClient();
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(timeoutMs);
            await client.ConnectAsync(address, port, cts.Token);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
