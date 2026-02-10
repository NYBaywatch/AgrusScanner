namespace AgrusScanner.Services;

public static class ServiceNameMap
{
    private static readonly Dictionary<int, string> Map = new()
    {
        [20] = "ftp-data", [21] = "ftp", [22] = "ssh", [23] = "telnet",
        [25] = "smtp", [53] = "dns", [67] = "dhcp", [68] = "dhcp",
        [69] = "tftp", [80] = "http", [110] = "pop3", [111] = "rpcbind",
        [119] = "nntp", [123] = "ntp", [135] = "msrpc", [137] = "netbios-ns",
        [138] = "netbios-dgm", [139] = "netbios-ssn", [143] = "imap",
        [161] = "snmp", [162] = "snmptrap", [179] = "bgp", [389] = "ldap",
        [443] = "https", [445] = "smb", [465] = "smtps", [500] = "isakmp",
        [514] = "syslog", [515] = "printer", [520] = "rip", [587] = "submission",
        [631] = "ipp", [636] = "ldaps", [993] = "imaps", [995] = "pop3s",
        [1080] = "socks", [1433] = "mssql", [1434] = "mssql-m",
        [1521] = "oracle", [1723] = "pptp", [2049] = "nfs",
        [2082] = "cpanel", [2083] = "cpanels", [2086] = "whm",
        [2087] = "whms", [3306] = "mysql", [3389] = "rdp",
        [5432] = "postgresql", [5900] = "vnc", [5901] = "vnc-1",
        [6379] = "redis", [8080] = "http-alt", [8443] = "https-alt",
        [8888] = "http-alt2", [9090] = "zeus-admin", [9200] = "elasticsearch",
        [27017] = "mongodb"
    };

    public static string GetServiceName(int port)
        => Map.TryGetValue(port, out var name) ? name : "unknown";
}
