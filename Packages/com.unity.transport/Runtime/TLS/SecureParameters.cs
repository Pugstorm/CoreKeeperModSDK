using System;
using Unity.Collections;
using Unity.Networking.Transport;

namespace Unity.Networking.Transport.TLS
{
    /// <summary>Client authentication policies (server only).</summary>
    public enum SecureClientAuthPolicy : uint
    {
        /// <summary>Client certificate is not requested (thus not verified).</summary>
        None = 0,
        /// <summary>Client certificate is requested, but not verified.</summary>
        Optional = 1,
        /// <summary>Client certificate is requested and verified.</summary>
        Required = 2,
    }

    /// <summary>
    /// Settings used to configure the secure protocol implementation.
    /// </summary>
    public struct SecureNetworkProtocolParameter : INetworkParameter
    {
        /// <summary>Root CA certificate (PEM format).</summary>
        public FixedPEMString         CACertificate;
        /// <summary>Server/client certificate (PEM format).</summary>
        public FixedPEMString         Certificate;
        /// <summary>Server/client private key (PEM format).</summary>
        public FixedPEMString         PrivateKey;
        /// <summary>Server/client certificate's common name.</summary>
        public FixedString512Bytes    Hostname;
        /// <summary>Client authentication policy (server only, defaults to optional).</summary>
        public SecureClientAuthPolicy ClientAuthenticationPolicy;

        public bool Validate() => true;
    }

    public static class SecureParameterExtensions
    {
        /// <summary>Set client security parameters (for WebSocket usage).</summary>
        /// <param name="serverName">Hostname of the server to connect to.</param>
        public static ref NetworkSettings WithSecureClientParameters(
            ref this NetworkSettings    settings,
            ref FixedString512Bytes     serverName)
        {
            var parameter = new SecureNetworkProtocolParameter
            {
                CACertificate               = default,
                Certificate                 = default,
                PrivateKey                  = default,
                Hostname                    = serverName,
                ClientAuthenticationPolicy  = SecureClientAuthPolicy.None,
            };

            settings.AddRawParameterStruct(ref parameter);

            return ref settings;
        }

        /// <summary>Set client security parameters (for WebSocket usage).</summary>
        /// <param name="serverName">Hostname of the server to connect to.</param>
        public static ref NetworkSettings WithSecureClientParameters(
            ref this NetworkSettings    settings,
            string                      serverName)
        {
            var parameter = new SecureNetworkProtocolParameter
            {
                CACertificate               = default,
                Certificate                 = default,
                PrivateKey                  = default,
                Hostname                    = serverName,
                ClientAuthenticationPolicy  = SecureClientAuthPolicy.None,
            };

            settings.AddRawParameterStruct(ref parameter);

            return ref settings;
        }

        /// <summary>Set client security parameters (server authentication only).</summary>
        /// <param name="caCertificate">CA certificate that signed the server's certificate (PEM format).</param>
        /// <param name="serverName">Common name (CN) in the server certificate.</param>
        public static ref NetworkSettings WithSecureClientParameters(
            ref this NetworkSettings    settings,
            ref FixedString4096Bytes    caCertificate,
            ref FixedString512Bytes     serverName)
        {
            var parameter = new SecureNetworkProtocolParameter
            {
                CACertificate               = new FixedPEMString(ref caCertificate),
                Certificate                 = default,
                PrivateKey                  = default,
                Hostname                    = serverName,
                ClientAuthenticationPolicy  = SecureClientAuthPolicy.None,
            };

            settings.AddRawParameterStruct(ref parameter);

            return ref settings;
        }

        /// <summary>Set client security parameters (server authentication only).</summary>
        /// <param name="caCertificate">CA certificate that signed the server's certificate (PEM format).</param>
        /// <param name="serverName">Common name (CN) in the server certificate.</param>
        public static ref NetworkSettings WithSecureClientParameters(
            ref this NetworkSettings    settings,
            ref FixedPEMString          caCertificate,
            ref FixedString512Bytes     serverName)
        {
            var parameter = new SecureNetworkProtocolParameter
            {
                CACertificate               = caCertificate,
                Certificate                 = default,
                PrivateKey                  = default,
                Hostname                    = serverName,
                ClientAuthenticationPolicy  = SecureClientAuthPolicy.None,
            };

            settings.AddRawParameterStruct(ref parameter);

            return ref settings;
        }

        /// <summary>Set client security parameters (server authentication only).</summary>
        /// <param name="caCertificate">CA certificate that signed the server's certificate (PEM format).</param>
        /// <param name="serverName">Common name (CN) in the server certificate.</param>
        public static ref NetworkSettings WithSecureClientParameters(
            ref this NetworkSettings    settings,
            string                      caCertificate,
            string                      serverName)
        {
            var parameter = new SecureNetworkProtocolParameter
            {
                CACertificate               = new FixedPEMString(caCertificate),
                Certificate                 = default,
                PrivateKey                  = default,
                Hostname                    = serverName,
                ClientAuthenticationPolicy  = SecureClientAuthPolicy.None,
            };

            settings.AddRawParameterStruct(ref parameter);

            return ref settings;
        }

        /// <summary>Set client security parameters (for client authentication).</summary>
        /// <param name="certificate">Client's certificate (PEM format).</param>
        /// <param name="privateKey">Client's private key (PEM format).</param>
        /// <param name="caCertificate">CA certificate that signed the server's certificate (PEM format).</param>
        /// <param name="serverName">Common name (CN) in the server certificate.</param>
        public static ref NetworkSettings WithSecureClientParameters(
            ref this NetworkSettings    settings,
            ref FixedString4096Bytes    certificate,
            ref FixedString4096Bytes    privateKey,
            ref FixedString4096Bytes    caCertificate,
            ref FixedString512Bytes     serverName)
        {
            var parameter = new SecureNetworkProtocolParameter
            {
                CACertificate               = new FixedPEMString(ref caCertificate),
                Certificate                 = new FixedPEMString(ref certificate),
                PrivateKey                  = new FixedPEMString(ref privateKey),
                Hostname                    = serverName,
                ClientAuthenticationPolicy  = SecureClientAuthPolicy.None,
            };

            settings.AddRawParameterStruct(ref parameter);

            return ref settings;
        }

        /// <summary>Set client security parameters (for client authentication).</summary>
        /// <param name="certificate">Client's certificate (PEM format).</param>
        /// <param name="privateKey">Client's private key (PEM format).</param>
        /// <param name="caCertificate">CA certificate that signed the server's certificate (PEM format).</param>
        /// <param name="serverName">Common name (CN) in the server certificate.</param>
        public static ref NetworkSettings WithSecureClientParameters(
            ref this NetworkSettings    settings,
            ref FixedPEMString          certificate,
            ref FixedPEMString          privateKey,
            ref FixedPEMString          caCertificate,
            ref FixedString512Bytes     serverName)
        {
            var parameter = new SecureNetworkProtocolParameter
            {
                CACertificate               = caCertificate,
                Certificate                 = certificate,
                PrivateKey                  = privateKey,
                Hostname                    = serverName,
                ClientAuthenticationPolicy  = SecureClientAuthPolicy.None,
            };

            settings.AddRawParameterStruct(ref parameter);

            return ref settings;
        }

        /// <summary>Set client security parameters (for client authentication).</summary>
        /// <param name="certificate">Client's certificate (PEM format).</param>
        /// <param name="privateKey">Client's private key (PEM format).</param>
        /// <param name="caCertificate">CA certificate that signed the server's certificate (PEM format).</param>
        /// <param name="serverName">Common name (CN) in the server certificate.</param>
        public static ref NetworkSettings WithSecureClientParameters(
            ref this NetworkSettings    settings,
            string                      certificate,
            string                      privateKey,
            string                      caCertificate,
            string                      serverName)
        {
            var parameter = new SecureNetworkProtocolParameter
            {
                CACertificate               = new FixedPEMString(caCertificate),
                Certificate                 = new FixedPEMString(certificate),
                PrivateKey                  = new FixedPEMString(privateKey),
                Hostname                    = serverName,
                ClientAuthenticationPolicy  = SecureClientAuthPolicy.None,
            };

            settings.AddRawParameterStruct(ref parameter);

            return ref settings;
        }

        /// <summary>Set server security parameters (server authentication only).</summary>
        /// <param name="certificate">Server's certificate chain (PEM format).</param>
        /// <param name="privateKey">Server's private key (PEM format).</param>
        public static ref NetworkSettings WithSecureServerParameters(
            ref this NetworkSettings    settings,
            ref FixedString4096Bytes    certificate,
            ref FixedString4096Bytes    privateKey)
        {
            var parameter = new SecureNetworkProtocolParameter
            {
                CACertificate               = default,
                Certificate                 = new FixedPEMString(ref certificate),
                PrivateKey                  = new FixedPEMString(ref privateKey),
                Hostname                    = default,
                ClientAuthenticationPolicy  = SecureClientAuthPolicy.None,
            };

            settings.AddRawParameterStruct(ref parameter);

            return ref settings;
        }

        /// <summary>Set server security parameters (server authentication only).</summary>
        /// <param name="certificate">Server's certificate chain (PEM format).</param>
        /// <param name="privateKey">Server's private key (PEM format).</param>
        public static ref NetworkSettings WithSecureServerParameters(
            ref this NetworkSettings    settings,
            ref FixedPEMString          certificate,
            ref FixedPEMString          privateKey)
        {
            var parameter = new SecureNetworkProtocolParameter
            {
                CACertificate               = default,
                Certificate                 = certificate,
                PrivateKey                  = privateKey,
                Hostname                    = default,
                ClientAuthenticationPolicy  = SecureClientAuthPolicy.None,
            };

            settings.AddRawParameterStruct(ref parameter);

            return ref settings;
        }

        /// <summary>Set server security parameters (server authentication only).</summary>
        /// <param name="certificate">Server's certificate chain (PEM format).</param>
        /// <param name="privateKey">Server's private key (PEM format).</param>
        public static ref NetworkSettings WithSecureServerParameters(
            ref this NetworkSettings    settings,
            string                      certificate,
            string                      privateKey)
        {
            var parameter = new SecureNetworkProtocolParameter
            {
                CACertificate               = default,
                Certificate                 = new FixedPEMString(certificate),
                PrivateKey                  = new FixedPEMString(privateKey),
                Hostname                    = default,
                ClientAuthenticationPolicy  = SecureClientAuthPolicy.None,
            };

            settings.AddRawParameterStruct(ref parameter);

            return ref settings;
        }

        /// <summary>Set server security parameters (for client authentication).</summary>
        /// <param name="certificate">Server's certificate chain (PEM format).</param>
        /// <param name="privateKey">Server's private key (PEM format).</param>
        /// <param name="caCertificate">CA certificate that signed the client certificates (PEM format).</param>
        /// <param name="clientName">Common name (CN) in the client certificates.</param>
        /// <param name="clientAuthenticationPolicy">Client authentication policy.</param>
        public static ref NetworkSettings WithSecureServerParameters(
            ref this NetworkSettings    settings,
            ref FixedString4096Bytes    certificate,
            ref FixedString4096Bytes    privateKey,
            ref FixedString4096Bytes    caCertificate,
            ref FixedString512Bytes     clientName,
            SecureClientAuthPolicy      clientAuthenticationPolicy = SecureClientAuthPolicy.Required)
        {
            var parameter = new SecureNetworkProtocolParameter
            {
                CACertificate               = new FixedPEMString(ref caCertificate),
                Certificate                 = new FixedPEMString(ref certificate),
                PrivateKey                  = new FixedPEMString(ref privateKey),
                Hostname                    = clientName,
                ClientAuthenticationPolicy  = clientAuthenticationPolicy,
            };

            settings.AddRawParameterStruct(ref parameter);

            return ref settings;
        }

        /// <summary>Set server security parameters (for client authentication).</summary>
        /// <param name="certificate">Server's certificate chain (PEM format).</param>
        /// <param name="privateKey">Server's private key (PEM format).</param>
        /// <param name="caCertificate">CA certificate that signed the client certificates (PEM format).</param>
        /// <param name="clientName">Common name (CN) in the client certificates.</param>
        /// <param name="clientAuthenticationPolicy">Client authentication policy.</param>
        public static ref NetworkSettings WithSecureServerParameters(
            ref this NetworkSettings    settings,
            ref FixedPEMString          certificate,
            ref FixedPEMString          privateKey,
            ref FixedPEMString          caCertificate,
            ref FixedString512Bytes     clientName,
            SecureClientAuthPolicy      clientAuthenticationPolicy = SecureClientAuthPolicy.Required)
        {
            var parameter = new SecureNetworkProtocolParameter
            {
                CACertificate               = caCertificate,
                Certificate                 = certificate,
                PrivateKey                  = privateKey,
                Hostname                    = clientName,
                ClientAuthenticationPolicy  = clientAuthenticationPolicy,
            };

            settings.AddRawParameterStruct(ref parameter);

            return ref settings;
        }

        /// <summary>Set server security parameters (for client authentication).</summary>
        /// <param name="certificate">Server's certificate chain (PEM format).</param>
        /// <param name="privateKey">Server's private key (PEM format).</param>
        /// <param name="caCertificate">CA certificate that signed the client certificates (PEM format).</param>
        /// <param name="clientName">Common name (CN) in the client certificates.</param>
        /// <param name="clientAuthenticationPolicy">Client authentication policy.</param>
        public static ref NetworkSettings WithSecureServerParameters(
            ref this NetworkSettings    settings,
            string                      certificate,
            string                      privateKey,
            string                      caCertificate,
            string                      clientName,
            SecureClientAuthPolicy      clientAuthenticationPolicy = SecureClientAuthPolicy.Required)
        {
            var parameter = new SecureNetworkProtocolParameter
            {
                CACertificate               = new FixedPEMString(caCertificate),
                Certificate                 = new FixedPEMString(certificate),
                PrivateKey                  = new FixedPEMString(privateKey),
                Hostname                    = clientName,
                ClientAuthenticationPolicy  = clientAuthenticationPolicy,
            };

            settings.AddRawParameterStruct(ref parameter);

            return ref settings;
        }

        public static SecureNetworkProtocolParameter GetSecureParameters(ref this NetworkSettings settings)
        {
            if (!settings.TryGet<SecureNetworkProtocolParameter>(out var parameters))
            {
                throw new System.InvalidOperationException($"Can't extract Secure parameters: {nameof(SecureNetworkProtocolParameter)} must be provided to the {nameof(NetworkSettings)}");
            }

            return parameters;
        }
    }
}
