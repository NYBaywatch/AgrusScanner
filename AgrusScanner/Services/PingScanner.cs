using System.Net;
using System.Net.NetworkInformation;

namespace AgrusScanner.Services;

public class PingScanner
{
    public async Task<(bool alive, long? roundtripMs)> PingAsync(
        IPAddress address, int timeoutMs, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        try
        {
            using var ping = new Ping();
            var reply = await ping.SendPingAsync(address, timeoutMs);
            if (reply.Status == IPStatus.Success)
                return (true, reply.RoundtripTime);
        }
        catch (PingException)
        {
            // Host unreachable or network error
        }
        catch (OperationCanceledException)
        {
            throw;
        }

        return (false, null);
    }

    public async Task SweepAsync(
        IReadOnlyList<IPAddress> addresses,
        int timeoutMs,
        int maxConcurrency,
        Action<IPAddress, bool, long?> onResult,
        CancellationToken ct)
    {
        using var semaphore = new SemaphoreSlim(maxConcurrency);
        var tasks = addresses.Select(async ip =>
        {
            await semaphore.WaitAsync(ct);
            try
            {
                var (alive, ms) = await PingAsync(ip, timeoutMs, ct);
                onResult(ip, alive, ms);
            }
            finally
            {
                semaphore.Release();
            }
        });

        await Task.WhenAll(tasks);
    }
}
