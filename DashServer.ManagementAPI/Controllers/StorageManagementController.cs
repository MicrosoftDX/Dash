using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using System.Web;
using System.Web.Configuration;
using System.Web.Http;
using System.Web.Http.Results;
using DashServer.ManagementAPI.Models;
using DashServer.ManagementAPI.Utils;
using Microsoft.Azure;
using Microsoft.WindowsAzure.Management.Storage;
using Microsoft.WindowsAzure.Management.Storage.Models;

namespace DashServer.ManagementAPI.Controllers
{
    public class StorageManagementController : ApiController
    {
        private readonly string _subscriptionId = ConfigurationHelper.GetSetting("SubscriptionId");
        private readonly string _certificateBase64 = ConfigurationHelper.GetSetting("CertificateBase64");

        [HttpGet]
        public async Task<IEnumerable<StorageAccount>> ListAccounts()
        {
             using (var storageClient = new StorageManagementClient(new CertificateCloudCredentials(_subscriptionId,  new X509Certificate2(
                                Convert.FromBase64String(_certificateBase64)))))
             {
                 var result = await storageClient.StorageAccounts.ListAsync();
                 return result.StorageAccounts;
             }
        }
        [HttpPost]
        public async Task<HttpResponseMessage> CreateAccount(CreateAccountRequest request)
        {
            // Create storage client
            using (var storageClient = new StorageManagementClient(new CertificateCloudCredentials(_subscriptionId, new X509Certificate2(
                                Convert.FromBase64String(_certificateBase64)))))
            {
                var response = await storageClient.StorageAccounts.CreateAsync(
                    new StorageAccountCreateParameters
                    {
                        AccountType = request.AccountType,
                        Location = request.Location,
                        Name = request.AccountName
                    });
                return new HttpResponseMessage(response.HttpStatusCode);
            }
        }

        [HttpDelete]
        public async Task<HttpResponseMessage> DeleteAccount(DeleteAccountRequest request)
        {
            using (var storageClient = new StorageManagementClient(new CertificateCloudCredentials(_subscriptionId, new X509Certificate2(
                                Convert.FromBase64String(_certificateBase64)))))
            {
                var response = await storageClient.StorageAccounts.DeleteAsync(request.AccountName);
                return new HttpResponseMessage(response.StatusCode);
            }
        }

        [HttpPut]
        public async Task<HttpResponseMessage> GenerateNewKey(GenerateNewKeyRequest request)
        {
            using (var storageClient = new StorageManagementClient(new CertificateCloudCredentials(_subscriptionId, new X509Certificate2(
                                Convert.FromBase64String(_certificateBase64)))))
            {
                var response = await storageClient.StorageAccounts.RegenerateKeysAsync(
                    new StorageAccountRegenerateKeysParameters()
                    {
                        KeyType = request.AccountKeyType.Equals("Primary", StringComparison.OrdinalIgnoreCase) ? StorageKeyType.Primary : StorageKeyType.Secondary,
                        Name = request.AccountName
                    });
                return new HttpResponseMessage(response.StatusCode);
            }
        }

        [HttpGet]
        public async Task<HttpResponseMessage> GetAccountKey(string accountName)
        {
            using (var storageClient = new StorageManagementClient(new CertificateCloudCredentials(_subscriptionId, new X509Certificate2(
                                Convert.FromBase64String(_certificateBase64)))))
            {
                var response = await storageClient.StorageAccounts.GetKeysAsync(accountName);
                
                return new HttpResponseMessage(response.StatusCode);
            }
        }
    }
}
