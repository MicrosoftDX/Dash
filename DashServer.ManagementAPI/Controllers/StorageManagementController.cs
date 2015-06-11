using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web;
using System.Web.Http;
using System.Web.Http.Results;
using DashServer.ManagementAPI.Models;
using Microsoft.Azure;
using Microsoft.WindowsAzure.Management.Storage;
using Microsoft.WindowsAzure.Management.Storage.Models;

namespace DashServer.ManagementAPI.Controllers
{
    public class StorageManagementController : ApiController
    {
        [HttpGet]
        public async Task<IEnumerable<StorageAccount>> ListAccounts()
        {
             using (var storageClient = new StorageManagementClient())
             {
                 var result = await storageClient.StorageAccounts.ListAsync();
                 return result.StorageAccounts;
             }
        }
        [HttpPost]
        public async Task<HttpResponseMessage> CreateAccount(CreateAccountRequest request)
        {
            // Create storage client
            using (var storageClient = new StorageManagementClient())
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
            using (var storageClient = new StorageManagementClient())
            {
                var response = await storageClient.StorageAccounts.DeleteAsync(request.AccountName);
                return new HttpResponseMessage(response.StatusCode);
            }
        }

        [HttpPut]
        public async Task<HttpResponseMessage> GenerateNewKey(GenerateNewKeyRequest request)
        {
            using (var storageClient = new StorageManagementClient())
            {
                var response = await storageClient.StorageAccounts.RegenerateKeysAsync(
                    new StorageAccountRegenerateKeysParameters()
                    {
                        KeyType = request.AccountKeyType.Equals("Primary") ? StorageKeyType.Primary : StorageKeyType.Secondary,
                        Name = request.AccountName
                    });
                return new HttpResponseMessage(response.StatusCode);
            }
        }
    }
}
