using System.Net;
using System.Net.Sockets;
using Trail.Application.Features.Integrations;

namespace Trail.Infrastructure.Integrations;

/// <summary>
/// Connect callback for the webhook HTTP client. It resolves the target name itself and connects only to public
/// addresses, so a name that points at the internal network (or flips to it between our check and the connection,
/// "DNS rebinding") cannot be reached. The address that was checked is the address that is connected to.
/// </summary>
internal static class SsrfSafeConnect
{
    public static async ValueTask<Stream> ConnectAsync(SocketsHttpConnectionContext context, CancellationToken ct)
    {
        var host = context.DnsEndPoint.Host;
        var resolved = IPAddress.TryParse(host.Trim('[', ']'), out var literal)
            ? [literal]
            : await Dns.GetHostAddressesAsync(host, ct);

        var allowed = resolved.Where(WebhookTargetGuard.IsPublic).ToList();
        if (allowed.Count == 0)
            throw new HttpRequestException($"Refusing to connect to '{host}': it does not resolve to a public address.");

        Exception? last = null;
        foreach (var address in allowed)
        {
            var socket = new Socket(address.AddressFamily, SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
            try
            {
                await socket.ConnectAsync(new IPEndPoint(address, context.DnsEndPoint.Port), ct);
                return new NetworkStream(socket, ownsSocket: true);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                socket.Dispose();
                last = ex;
            }
        }

        throw new HttpRequestException($"Could not connect to '{host}'.", last);
    }
}
