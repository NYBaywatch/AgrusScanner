using System.Net;

namespace AgrusScanner.Services;

public static class IpRangeParser
{
    public static List<IPAddress> Parse(string input)
    {
        input = input.Trim();

        // CIDR notation: 192.168.1.0/24
        if (input.Contains('/'))
            return ParseCidr(input);

        // Range notation: 192.168.1.1-192.168.1.254
        if (input.Contains('-'))
            return ParseRange(input);

        // Single IP
        if (IPAddress.TryParse(input, out var single))
            return [single];

        throw new ArgumentException($"Invalid IP range format: {input}");
    }

    private static List<IPAddress> ParseCidr(string cidr)
    {
        var parts = cidr.Split('/');
        if (parts.Length != 2 || !IPAddress.TryParse(parts[0], out var network) || !int.TryParse(parts[1], out var prefixLen))
            throw new ArgumentException($"Invalid CIDR notation: {cidr}");

        if (prefixLen < 0 || prefixLen > 32)
            throw new ArgumentException($"Invalid prefix length: {prefixLen}");

        var networkBytes = network.GetAddressBytes();
        var networkUint = BytesToUint(networkBytes);

        var hostBits = 32 - prefixLen;
        var hostCount = 1u << hostBits;

        var results = new List<IPAddress>();
        // Skip network address (first) and broadcast (last) for /31 and larger
        var start = hostCount > 2 ? 1u : 0u;
        var end = hostCount > 2 ? hostCount - 1 : hostCount;

        for (var i = start; i < end; i++)
        {
            var ip = networkUint + i;
            results.Add(new IPAddress(UintToBytes(ip)));
        }

        return results;
    }

    private static List<IPAddress> ParseRange(string range)
    {
        var parts = range.Split('-');
        if (parts.Length != 2)
            throw new ArgumentException($"Invalid range format: {range}");

        if (!IPAddress.TryParse(parts[0].Trim(), out var startIp))
            throw new ArgumentException($"Invalid start IP: {parts[0]}");

        // Support short form: 192.168.1.1-254
        IPAddress endIp;
        var endPart = parts[1].Trim();
        if (IPAddress.TryParse(endPart, out var fullEnd))
        {
            endIp = fullEnd;
        }
        else if (byte.TryParse(endPart, out var lastOctet))
        {
            var startBytes = startIp.GetAddressBytes();
            startBytes[3] = lastOctet;
            endIp = new IPAddress(startBytes);
        }
        else
        {
            throw new ArgumentException($"Invalid end IP: {endPart}");
        }

        var startUint = BytesToUint(startIp.GetAddressBytes());
        var endUint = BytesToUint(endIp.GetAddressBytes());

        if (endUint < startUint)
            throw new ArgumentException("End IP must be >= start IP");

        if (endUint - startUint > 65536)
            throw new ArgumentException("Range too large (max 65536 addresses)");

        var results = new List<IPAddress>();
        for (var i = startUint; i <= endUint; i++)
            results.Add(new IPAddress(UintToBytes(i)));

        return results;
    }

    private static uint BytesToUint(byte[] bytes)
        => (uint)(bytes[0] << 24 | bytes[1] << 16 | bytes[2] << 8 | bytes[3]);

    private static byte[] UintToBytes(uint value)
        => [(byte)(value >> 24), (byte)(value >> 16), (byte)(value >> 8), (byte)value];
}
