using System.Net;
using System.Net.Sockets;

namespace Trail.Application.Features.Integrations;

/// <summary>
/// Server-side request forgery guard for webhook targets. Customers choose the URL we call, so it must not be
/// able to reach our own network, cloud metadata endpoints or loopback. <see cref="Validate"/> checks the URL when
/// it is saved; <see cref="IsPublic"/> is applied to the resolved address again at connect time, which is what
/// actually defeats names that resolve to private addresses and DNS rebinding.
/// </summary>
public static class WebhookTargetGuard
{
    /// <summary>Returns a message when the URL may not be used as a webhook target, else null.</summary>
    public static string? Validate(string? url, bool allowInsecure)
    {
        if (string.IsNullOrWhiteSpace(url)) return "A URL is required.";
        if (!Uri.TryCreate(url.Trim(), UriKind.Absolute, out var uri)) return "Enter an absolute URL.";

        var schemeOk = uri.Scheme == Uri.UriSchemeHttps || (allowInsecure && uri.Scheme == Uri.UriSchemeHttp);
        if (!schemeOk) return "The URL must use https.";
        if (!string.IsNullOrEmpty(uri.UserInfo)) return "The URL must not contain credentials.";
        if (uri.HostNameType == UriHostNameType.Unknown || string.IsNullOrEmpty(uri.Host)) return "The URL has no valid host.";
        if (allowInsecure) return null;

        var host = uri.Host.TrimEnd('.');
        if (host.Equals("localhost", StringComparison.OrdinalIgnoreCase)
            || host.EndsWith(".localhost", StringComparison.OrdinalIgnoreCase))
            return "The URL must not point at localhost.";

        // .NET normalizes decimal, octal and hex IPv4 spellings (http://2130706433) to dotted form in Uri.Host.
        if (IPAddress.TryParse(host.Trim('[', ']'), out var literal) && !IsPublic(literal))
            return "The URL must not point at a private, loopback or link-local address.";

        return null;
    }

    /// <summary>True when the address is routable on the public internet (not private, loopback, link-local, CGNAT, reserved).</summary>
    public static bool IsPublic(IPAddress address)
    {
        if (address.IsIPv4MappedToIPv6) address = address.MapToIPv4();

        return address.AddressFamily switch
        {
            AddressFamily.InterNetwork => IsPublicV4(address.GetAddressBytes()),
            AddressFamily.InterNetworkV6 => IsPublicV6(address),
            _ => false,
        };
    }

    private static bool IsPublicV4(ReadOnlySpan<byte> b) => !(
        b[0] == 0                                               // 0.0.0.0/8 "this network"
        || b[0] == 10                                           // 10.0.0.0/8
        || (b[0] == 100 && b[1] is >= 64 and <= 127)            // 100.64.0.0/10 carrier-grade NAT
        || b[0] == 127                                          // loopback
        || (b[0] == 169 && b[1] == 254)                         // link-local, includes cloud metadata 169.254.169.254
        || (b[0] == 172 && b[1] is >= 16 and <= 31)             // 172.16.0.0/12
        || (b[0] == 192 && b[1] == 0 && b[2] == 0)              // 192.0.0.0/24 IETF protocol assignments
        || (b[0] == 192 && b[1] == 0 && b[2] == 2)              // TEST-NET-1
        || (b[0] == 192 && b[1] == 168)                         // 192.168.0.0/16
        || (b[0] == 198 && b[1] is 18 or 19)                    // 198.18.0.0/15 benchmarking
        || (b[0] == 198 && b[1] == 51 && b[2] == 100)           // TEST-NET-2
        || (b[0] == 203 && b[1] == 0 && b[2] == 113)            // TEST-NET-3
        || b[0] >= 224);                                        // multicast, reserved, broadcast

    private static bool IsPublicV6(IPAddress address)
    {
        if (address.Equals(IPAddress.IPv6None) || address.Equals(IPAddress.IPv6Loopback)) return false;
        if (address.IsIPv6LinkLocal || address.IsIPv6SiteLocal || address.IsIPv6Multicast || address.IsIPv6UniqueLocal) return false;

        var b = address.GetAddressBytes();

        // ::/96 IPv4-compatible (deprecated) and 64:ff9b::/96 NAT64: judge by the embedded IPv4 address.
        var compatible = b.AsSpan(0, 12).IndexOfAnyExcept((byte)0) < 0;
        var nat64 = b[0] == 0x00 && b[1] == 0x64 && b[2] == 0xff && b[3] == 0x9b && b.AsSpan(4, 8).IndexOfAnyExcept((byte)0) < 0;
        if (compatible || nat64) return IsPublicV4(b.AsSpan(12, 4));

        // 2002::/16 6to4 embeds an IPv4 address in bytes 2..5.
        if (b[0] == 0x20 && b[1] == 0x02) return IsPublicV4(b.AsSpan(2, 4));

        // 2001::/32 Teredo and 2001:db8::/32 documentation.
        if (b[0] == 0x20 && b[1] == 0x01 && b[2] == 0x00 && b[3] == 0x00) return false;
        if (b[0] == 0x20 && b[1] == 0x01 && b[2] == 0x0d && b[3] == 0xb8) return false;

        return true;
    }
}
