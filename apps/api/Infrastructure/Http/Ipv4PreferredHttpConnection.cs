using System.Net;
using System.Net.Sockets;

namespace Api.Infrastructure.Http;

public static class Ipv4PreferredHttpConnection
{
    public static SocketsHttpHandler CreateHandler() => new()
    {
        ConnectCallback = ConnectAsync
    };

    public static async ValueTask<Stream> ConnectAsync(
        SocketsHttpConnectionContext context,
        CancellationToken cancellationToken)
    {
        var endpoint = context.DnsEndPoint;
        IPAddress[] addresses;
        try
        {
            addresses = await Dns.GetHostAddressesAsync(
                endpoint.Host,
                cancellationToken);
        }
        catch (SocketException exception)
        {
            throw new HttpRequestException(
                $"DNS resolution failed for {endpoint.Host}:{endpoint.Port}.",
                exception);
        }

        var ipv4Addresses = addresses
            .Where(address => address.AddressFamily == AddressFamily.InterNetwork)
            .Distinct()
            .ToArray();
        if (ipv4Addresses.Length == 0)
        {
            throw new HttpRequestException(
                $"DNS resolution for {endpoint.Host}:{endpoint.Port} returned no IPv4 addresses.");
        }

        var failures = new List<SocketException>(ipv4Addresses.Length);
        foreach (var address in ipv4Addresses)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var socket = new Socket(
                AddressFamily.InterNetwork,
                SocketType.Stream,
                ProtocolType.Tcp)
            {
                NoDelay = true
            };

            try
            {
                await socket.ConnectAsync(
                    new IPEndPoint(address, endpoint.Port),
                    cancellationToken);
                return new NetworkStream(socket, ownsSocket: true);
            }
            catch (SocketException exception)
            {
                failures.Add(exception);
                socket.Dispose();
            }
            catch
            {
                socket.Dispose();
                throw;
            }
        }

        throw new HttpRequestException(
            $"Could not connect to {endpoint.Host}:{endpoint.Port} using any of its {ipv4Addresses.Length} resolved IPv4 addresses.",
            new AggregateException(failures));
    }
}