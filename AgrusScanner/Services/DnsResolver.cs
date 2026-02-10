using System.Net;

namespace AgrusScanner.Services;

public class DnsResolver
{
    public async Task<string> ResolveAsync(IPAddress address, CancellationToken ct)
    {
        try
        {
            ct.ThrowIfCancellationRequested();
            var entry = await Dns.GetHostEntryAsync(address.ToString());
            return entry.HostName;
        }
        catch
        {
            return string.Empty;
        }
    }
}
