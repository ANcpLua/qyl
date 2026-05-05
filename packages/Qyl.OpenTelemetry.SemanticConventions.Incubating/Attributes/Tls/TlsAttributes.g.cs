

#nullable enable

namespace Qyl.OpenTelemetry.SemanticConventions.Incubating.Attributes.Tls;

public static class TlsAttributes
{
    public const string Cipher = "tls.cipher";

    public const string ClientCertificate = "tls.client.certificate";

    public const string ClientCertificateChain = "tls.client.certificate_chain";

    public const string ClientHashMd5 = "tls.client.hash.md5";

    public const string ClientHashSha1 = "tls.client.hash.sha1";

    public const string ClientHashSha256 = "tls.client.hash.sha256";

    public const string ClientIssuer = "tls.client.issuer";

    public const string ClientJa3 = "tls.client.ja3";

    public const string ClientNotAfter = "tls.client.not_after";

    public const string ClientNotBefore = "tls.client.not_before";

    [global::System.Obsolete("Replaced by server.address.", false)]
    public const string ClientServerName = "tls.client.server_name";

    public const string ClientSubject = "tls.client.subject";

    public const string ClientSupportedCiphers = "tls.client.supported_ciphers";

    public const string Curve = "tls.curve";

    public const string Established = "tls.established";

    public const string NextProtocol = "tls.next_protocol";

    public const string ProtocolName = "tls.protocol.name";

    public static class ProtocolNameValues
    {
        public const string Ssl = "ssl";

        public const string Tls = "tls";
    }

    public const string ProtocolVersion = "tls.protocol.version";

    public const string Resumed = "tls.resumed";

    public const string ServerCertificate = "tls.server.certificate";

    public const string ServerCertificateChain = "tls.server.certificate_chain";

    public const string ServerHashMd5 = "tls.server.hash.md5";

    public const string ServerHashSha1 = "tls.server.hash.sha1";

    public const string ServerHashSha256 = "tls.server.hash.sha256";

    public const string ServerIssuer = "tls.server.issuer";

    public const string ServerJa3s = "tls.server.ja3s";

    public const string ServerNotAfter = "tls.server.not_after";

    public const string ServerNotBefore = "tls.server.not_before";

    public const string ServerSubject = "tls.server.subject";
}
