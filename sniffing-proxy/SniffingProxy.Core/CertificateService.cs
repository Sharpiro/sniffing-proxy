using Newtonsoft.Json;
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
                var parsedParams = JsonConvert.DeserializeObject<RSAParameters>(RsaParams);
                rsa.ImportParameters(parsedParams);
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
                    //random.GetBytes(serialNumber);

                    var notBefore = new DateTime(2018, 07, 10);
                    var notAfter = new DateTime(2019, 01, 01);
                    // https://github.com/dotnet/corefx/issues/24454#issuecomment-388231655
                    using (var corruptFakeCert = req.Create(rootCert, notBefore, notAfter, serialNumber).CopyWithPrivateKey(rsa))
                    {
                        var fixedFakeCert = new X509Certificate2(corruptFakeCert.Export(X509ContentType.Pkcs12));
                        return fixedFakeCert;
                    }
                }
            }
        }

        const string RsaParams =
            @"{
  ""D"": ""muf/tPE37xjzrO/Y1Bk7n1xFEBIfjqc0ooPKX7HsI7YQRsDvCzsi5n9l4NoI74TVLoKkTxwNGi1xlP6BeIWG3ocBbrtbYqBaBR6Wu/wPJnjAomBaj90e1t3jPQQqS6k9QPXDgkP5Ozbli8oKUBDJ/ZTFcP0aEhM1VOkGGNeeNcF+K+7nI4DxRZHm722O0ecaX9UK/b30+ZbordnnLgM7z9REzOvNSS68yxjeAbHeR9zdIwuBo13FD3lOGAzwybXui53JYyGHG6R8D/CWlQgI9BnAkmkUuXxeAZ3gDSiYSiNwYYsB/fOfQSzFrw6D6yCUvPne9hzH0v0XdKjti3cKjo4VJDSRFscodJMeiHoWMglEHD8NPgN7G2VZm/AJGN3LwEmE+aaYnxc+6XBk6OfRReio8yBE1Zvpo45TS1Pa5Hti5F4vFMTcp6UlbbEHMsU4BthxsMrTwYI+yqKVI3NwLJK9TG0JghPYEX/M9dztAkS3Q58ma7+jzwTf5UGSGskZ4FsL0qXDfpD06zV0gNbLSi/MKgUQe6CuhOXAQRwDn9qDH+GrUi3bxlGSChaGd5jGbYYOpkFMoYitexjwK5foZzjwI6lBO89VtrGBgA6oqoPdHPzk74AhYYFWCI6UIJLEulu71K+6KR+RmrnODQpm1lgPrXSqCUqByfe3DSNRd9U="",
  ""DP"": ""XSjegVydN9rKxsbsdyEYzcM2Rg8Dynx/maEDgDqtI7RKxYAF5wmz3t9ehZhySN2tMGa1uaxoOd0GOisM47R9IzxgCibnxXYbXtf/5/DUThQzSeqhJVLp03gGMu4wHaLLM2hiKPsOEUYAKYgXY5VQfg1C/KQyhBlgN2jfyx+X0Hl2hZ/TnK+c4tPSYAf5XPevWm+lqRR+miiVrzXHouRnrRun5gWTQ7n1FV9D0zAI/lFRG3mVUlr7/WWJU7o8TMlLlnux98LDpxR2SnoxtCrg9aD3PJ1hUd4riHTzeoswhMaez8rNoaqHYAUSAtljTlkqAXmYWoNYu3FrOhV6LaKsOw=="",
  ""DQ"": ""AAUeomcLz4NcZ3zo/2OumbsXrhZqdr1DFaYBtQTaH09WrhTe1FQBsdjHQnIX2wiKwCUPC8gRrRN8p7aErmXtGuttFThDrNpDek3AwewvqeEmMPwDxIpJe81Ih5RjM28D35ydAYlrdLh1+L9k5IP502kCRYdcZps3x836DRtX/JotoJVR1rimxZuE8XY73NlPUdUgKooUX5Mp12yX0iAlvbtV5QnYir6+3i2LPQeWPd/1sef2j5WUOqI0YcC42o8709ABQrVhdedT6ApINycUp14w1U28CWZU28HHWCtlfLzyCM/Y6GaD3JmgynOW+Gc0r3bo6uH9+TTocGbEV/2wNQ=="",
  ""Exponent"": ""AQAB"",
  ""InverseQ"": ""eQMxSi7I/vas0GsaGnOPI6ujfWdXIOJSdEDoHKPG7vJrm4cIrl7AfWDhdOqDP3i8VyZG6pzwtlckn0lT+WSjVtGTTGOsqnk3eqpQs+fezrp+ONzkcJyxgrlUPN+5pS5B2q/NuqS0RwdF6WFjoPdI5M3XWMK6wsHzya7TJf+ieczapfNG43Ygf33Nv+yrSU7IzktmzcEv84nG8jXyJtXTcVumOInqE/7Ok9IhhXPKDoY6BPjXZW+24+Xt4eR77asQMZwXAC8evv3axUuialNsEk3miv8bDdZTBcqNM6hbCFhFH2HGTpAt4zUaWEOTprTViDQVIR2fBKEcDcHm30yBmw=="",
  ""Modulus"": ""0p6y3zrrrRaB2FLXV+ILQIYeM2AMlK7Lf5R8qMRRgIjrf4qtR9VftZJps+Q+O6co5bF2wpGJI5JAE0pUyq/umeTA/jxQOrJhvheyg6Jkh/wWFurUZEsByi5l9OAabVTpzlYIFWldNe9RC61JG5Ybk8DbRcObnbAWDWLGl3iKdPWVpcqeW5MFPqlEaJwjB3N4Tnk++lEdZP1kyDMOQoSxAM1qSV7iP0bH4y4h+QVDXqpqw1sltUUP+iC8O3qnTYpcU11bwQaig9ndYnHPEnsGrUBpREhq33GIVJK+ZcZMH1rx0by0uyEGhVyXxNXiBbTuquSZiHIoAzZ3tfG0ILgtoJMjjYfBzVeOUm5O/VwKr2jCqoXkBLS/15HWHSGVs0RPBG3Tg0F8Uz6p7pccJZ89qOUVuAucjnS72xZ4dVmVv0VmOrK9c+W9F75339un2SO+4oIktXO5fRxHWKMcAMevaHn3IvrL8W2PBqNfALqGvmPb/ayzP9jXR7BDFPYgW4P7B9Ivolm4Cy9/bKmfBLnQz0GwVffPu44mjO11FIC60sd0VXSlGDZJRd375BpCoh+NeP4RyjbQL8Ij3HrTH6NF94LVYFHZIDVYChF3/Q4r+DqIzTlknEcgZELQTV6DQKnZdZ3nohBd356Wq8HllAS39K8B+jB8wFwpWq4RnTE41hk="",
  ""P"": ""9tUjbe+SF0l1WdJuyKfUhPkjdf1ZUrpreQE4/O2PhfiYM10MS8wTFz937poQgtHkgxFQ/Yxp1sJOa/XuKjhqJMxrf1zolRi760tH+0nD0MToL8CxBVn2EXgAXJz5d67d8PJ9OZE6QwvNnN+FlI5+3DKkOmfB56K7US8dLQEolPdY/NjRhlduDZDAyvYcGU43T2Hbg7OdqfGx8L0uQoH08QReHe0ILeyHvWSUmxQcgbn3yl/9ziNmSkO8aKbWakQ/9Xou1k8A62UZit32MTVilhHp+LMR6FXVkpvNxyqUT5ndZRTj3S9sO4k/brms452ZQTWZb36JvfhuUp5fucFHhw=="",
  ""Q"": ""2nFA8l5tMfx5jWoMx6o8SDwyWWzb9E3Rg+SBF9tcPlTnQibdzlZPFC80ObeFZkwdTTLOhOjbhvSIh2YnGerOwVVw5CYvqA4YNIKc0u5YIq2wXiAXrQEDy2N6JyFNs+JwFMUGZ3+wpx2I5PCmz7k7I50W8FJT77sroE8uO4P9sbR10xIBv5ULDGzUh43Y3LHbaFVHCnxGlucZEopz/qT62Jq6n5jD4ZonZucYeIuTs0L6Rr6XW01eZ9X+LxnhWRCB+Nhzxgc2zE06XWA9N00TJAiVGDkwZQsDxdmodPFbEnZK+vQKkgsvlOYDZZH/si5x7rr3Js8zJZuNM3UgVrMdXw==""
}";
    }
}
