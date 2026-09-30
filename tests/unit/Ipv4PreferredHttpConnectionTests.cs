using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Api.Infrastructure.Http;
using Xunit;

namespace Api.Tests;

public sealed class Ipv4PreferredHttpConnectionTests
{
    [Fact]
    public async Task Connects_to_local_https_host_using_ipv4_and_preserves_hostname_validation()
    {
        using var certificate = CreateLocalhostCertificate();
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        var serverTask = ServeHttpsRequestAsync(
            listener,
            certificate,
            cancellation.Token);

        using var handler = Ipv4PreferredHttpConnection.CreateHandler();
        Assert.Null(handler.SslOptions.RemoteCertificateValidationCallback);
        var chainPolicy = new X509ChainPolicy
        {
            TrustMode = X509ChainTrustMode.CustomRootTrust,
            RevocationMode = X509RevocationMode.NoCheck
        };
        chainPolicy.CustomTrustStore.Add(certificate);
        handler.SslOptions.CertificateChainPolicy = chainPolicy;

        using var client = new HttpClient(handler)
        {
            Timeout = TimeSpan.FromSeconds(8)
        };
        var requestUri = new Uri($"https://localhost:{port}/health");

        HttpResponseMessage response;
        try
        {
            response = await client.GetAsync(requestUri, cancellation.Token);
        }
        catch
        {
            await serverTask;
            throw;
        }

        using (response)
        {
            var requestHeaders = await serverTask;

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal("ok", await response.Content.ReadAsStringAsync(cancellation.Token));
            Assert.Equal("https", requestUri.Scheme);
            Assert.Equal("localhost", requestUri.Host);
            Assert.Contains($"Host: localhost:{port}", requestHeaders, StringComparison.OrdinalIgnoreCase);
            Assert.Null(handler.SslOptions.RemoteCertificateValidationCallback);
        }
    }

    private static async Task<string> ServeHttpsRequestAsync(
        TcpListener listener,
        X509Certificate2 certificate,
        CancellationToken cancellationToken)
    {
        using var client = await listener.AcceptTcpClientAsync(cancellationToken);
        await using var tlsStream = new SslStream(client.GetStream(), leaveInnerStreamOpen: false);
        await tlsStream.AuthenticateAsServerAsync(
            new SslServerAuthenticationOptions
            {
                ServerCertificate = certificate,
                EnabledSslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13
            },
            cancellationToken);

        var request = new StringBuilder();
        var buffer = new byte[1024];
        while (!request.ToString().EndsWith("\r\n\r\n", StringComparison.Ordinal))
        {
            var bytesRead = await tlsStream.ReadAsync(buffer, cancellationToken);
            if (bytesRead == 0)
            {
                throw new IOException("Client closed the local TLS connection before sending headers.");
            }

            request.Append(Encoding.ASCII.GetString(buffer, 0, bytesRead));
            if (request.Length > 16 * 1024)
            {
                throw new IOException("Local test request headers exceeded the expected size.");
            }
        }

        var response = Encoding.ASCII.GetBytes(
            "HTTP/1.1 200 OK\r\nContent-Length: 2\r\nConnection: close\r\n\r\nok");
        await tlsStream.WriteAsync(response, cancellationToken);
        await tlsStream.FlushAsync(cancellationToken);
        return request.ToString();
    }

    private static X509Certificate2 CreateLocalhostCertificate()
    {
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest(
            "CN=localhost",
            rsa,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);
        var subjectAlternativeNames = new SubjectAlternativeNameBuilder();
        subjectAlternativeNames.AddDnsName("localhost");
        request.CertificateExtensions.Add(subjectAlternativeNames.Build());
        request.CertificateExtensions.Add(
            new X509BasicConstraintsExtension(false, false, 0, false));

        using var ephemeralCertificate = request.CreateSelfSigned(
            DateTimeOffset.UtcNow.AddMinutes(-1),
            DateTimeOffset.UtcNow.AddMinutes(5));
        const string temporaryPassword = "local-test-certificate";
        return X509CertificateLoader.LoadPkcs12(
            ephemeralCertificate.Export(X509ContentType.Pfx, temporaryPassword),
            temporaryPassword,
            X509KeyStorageFlags.UserKeySet | X509KeyStorageFlags.Exportable);
    }
}