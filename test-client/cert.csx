using System.Security.Cryptography.X509Certificates;

var userProfile = Environment.GetEnvironmentVariable("userprofile");
var certData = File.ReadAllBytes($@"{userProfile}\Desktop\temp\openssl\domain.crt");
var cert = new X509Certificate2(certData);
X509Store store = new X509Store(StoreName.Root, StoreLocation.CurrentUser);
store.Open(OpenFlags.ReadWrite);
store.Add(cert);
store.Close();