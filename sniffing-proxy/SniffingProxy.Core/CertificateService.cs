using System;
using System.Linq;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace SniffingProxy.Core
{
    public class CertificateService
    {
        public X509Certificate2 CreateFakeCertificate(string fakeCN, string rootCertSerialNumber)
        {
            using (var userStore = new X509Store(StoreName.Root, StoreLocation.CurrentUser))
            using (var rsa = RSA.Create(4096))
            using (var random = new RNGCryptoServiceProvider())
            {
                userStore.Open(OpenFlags.MaxAllowed);
                using (var rootCert = userStore.Certificates.Cast<X509Certificate2>().Single(c => c.SerialNumber.Equals(rootCertSerialNumber, StringComparison.InvariantCultureIgnoreCase)))
                {
                    var req = new CertificateRequest($"CN={fakeCN}", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

                    req.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, false));
                    req.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.KeyEncipherment | X509KeyUsageFlags.DigitalSignature, false));
                    req.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(new OidCollection {
                     new Oid("1.3.6.1.5.5.7.3.1") , // server auth
                     new Oid("1.3.6.1.5.5.7.3.2") // client auth
                     }, true));
                    req.CertificateExtensions.Add(new X509SubjectKeyIdentifierExtension(req.PublicKey, false));

                    var sanBuilder = new SubjectAlternativeNameBuilder();
                    sanBuilder.AddDnsName(fakeCN);
                    var sanExtension = sanBuilder.Build();
                    req.CertificateExtensions.Add(sanExtension);

                    var serialNumber = new byte[16];
                    random.GetBytes(serialNumber);

                    // https://github.com/dotnet/corefx/issues/24454#issuecomment-388231655
                    using (var corruptFakeCert = req.Create(rootCert, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddDays(90), serialNumber).CopyWithPrivateKey(rsa))
                    {
                        var fixedFakeCert = new X509Certificate2(corruptFakeCert.Export(X509ContentType.Pkcs12));
                        return fixedFakeCert;
                    }
                }
            }
        }
    }
}
