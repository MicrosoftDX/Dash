//     Copyright (c) Microsoft Corporation.  All rights reserved.

using System;
using System.Configuration;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Web;
using System.Web.Http;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Auth;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.Dash.Server.Utils;
using System.Threading.Tasks;
using System.Security.Cryptography;

namespace Microsoft.Dash.Server.Controllers
{
    public class CommonController : ApiController
    {
        protected Uri GetForwardingUri(HttpRequestBase request, CloudStorageAccount account, string containerName, bool useSas=false)
        {
            CloudBlobContainer container = GetContainerByName(account, containerName);

            UriBuilder forwardUri = new UriBuilder()
            {
                Scheme = request.Url.Scheme,
                Host = account.BlobEndpoint.Host,
                Path = container.Uri.AbsolutePath,
                Query = request.Url.Query
            };

            return forwardUri.Uri;
        }

        protected Uri GetRedirectUri(HttpRequestBase request, CloudStorageAccount account, string containerName, string blobName)
        {
            CloudBlobContainer container = GetContainerByName(account, containerName);
            UriBuilder forwardUri = new UriBuilder()
            {
                Scheme = request.Url.Scheme,
                Host = account.BlobEndpoint.Host,
                Path = containerName + "/" + blobName,
                Query = CalculateSASStringForContainer(request, container),
            };

            //creating redirection Uri
            if (!string.IsNullOrWhiteSpace(request.Url.Query))
            {
                forwardUri.Query += "&" + request.Url.Query.Substring(1);
            }

            return forwardUri.Uri;
        }

        protected Uri ForwardUriToNamespace(HttpRequestBase request)
        {
            UriBuilder forwardUri = new UriBuilder()
            {
                Scheme = request.Url.Scheme,
                Host = DashConfiguration.NamespaceAccount.BlobEndpoint.Host,
                Query = request.Url.Query
            };
            return forwardUri.Uri;
        }

        //calculates Shared Access Signature (SAS) string based on type of request (GET, HEAD, DELETE, PUT)
        protected SharedAccessBlobPolicy GetSasPolicy(HttpRequestBase request)
        {
            //Default to read only
            SharedAccessBlobPermissions permission = SharedAccessBlobPermissions.Read;
            if (request.HttpMethod == HttpMethod.Delete.ToString())
            {
                permission = SharedAccessBlobPermissions.Delete;
            }
            else if (request.HttpMethod == HttpMethod.Put.ToString())
            {
                permission = SharedAccessBlobPermissions.Write;
            }

            return new SharedAccessBlobPolicy()
            {
                Permissions = permission,
                SharedAccessStartTime = DateTime.Now.AddMinutes(-5),
                SharedAccessExpiryTime = DateTime.Now.AddMinutes(54)
            };
        }

        protected async Task<NamespaceBlob> CreateNamespaceBlobAsync(HttpRequestBase request, string container, string blob)
        {
            //create an namespace blob with hardcoded metadata
            var namespaceBlob = await FetchNamespaceBlobAsync(container, blob);
            bool exists = await namespaceBlob.ExistsAsync();
            if (exists && !String.IsNullOrWhiteSpace(namespaceBlob.BlobName))
            {
                return namespaceBlob;
                }
            else if (!exists)
            {
                await namespaceBlob.CreateAsync();
            }
            //getting storage account name and account key from file account, by using simple hashing algorithm to choose account storage
            var dataAccount = GetDataStorageAccountForBlob(blob);
            namespaceBlob.AccountName = dataAccount.Credentials.AccountName;
            namespaceBlob.Container = container;
            namespaceBlob.BlobName = blob;
            await namespaceBlob.SaveAsync();

            return namespaceBlob;
        }

        //getting storage account name and account key from file account, by using simple hashing algorithm to choose account storage
        protected CloudStorageAccount GetDataStorageAccountForBlob(string blobName)
        {
            return DashConfiguration.DataAccounts[GetHashCodeBucket(blobName, DashConfiguration.DataAccounts.Count)];
        }

        static int GetHashCodeBucket(string stringToHash, int numBuckets)
            {
            System.Diagnostics.Debug.Assert(!String.IsNullOrWhiteSpace(stringToHash));

            var hash = new SHA256CryptoServiceProvider();
            byte[] hashText = hash.ComputeHash(Encoding.UTF8.GetBytes(stringToHash));
                long hashCodeStart = BitConverter.ToInt64(hashText, 0);
                long hashCodeMedium = BitConverter.ToInt64(hashText, 8);
                long hashCodeEnd = BitConverter.ToInt64(hashText, 24);
            long hashCode = hashCodeStart ^ hashCodeMedium ^ hashCodeEnd;

            return (int)(Math.Abs(hashCode) % numBuckets);
        }

        //calculates SAS string to have access to a container
        protected string CalculateSASStringForContainer(HttpRequestBase request, CloudBlobContainer container)
        {
            SharedAccessBlobPolicy sasConstraints = GetSasPolicy(request);
            //Generate the shared access signature on the container, setting the constraints directly on the signature.
            return container.GetSharedAccessSignature(sasConstraints);
        }

        protected async Task<NamespaceBlob> FetchNamespaceBlobAsync(string container, string blobName)
        {
            return await NamespaceBlob.FetchForBlobAsync(GetBlobByName(DashConfiguration.NamespaceAccount, container, blobName));
        }

        protected CloudBlobContainer GetContainerByName(CloudStorageAccount account, string containerName)
        {
            CloudBlobClient client = account.CreateCloudBlobClient();
            return client.GetContainerReference(containerName);
        }

        protected CloudBlockBlob GetBlobByName(CloudStorageAccount account, string containerName, string blobName)
        {
            CloudBlobContainer container = GetContainerByName(account, containerName);
            return container.GetBlockBlobReference(blobName);
        }

        protected HttpRequestBase RequestFromContext(HttpContext context)
        {
            var curContext = new HttpContextWrapper(context);
            return curContext.Request;
        }

        protected HttpResponseMessage CreateResponse<T>(T result)
        {
            return CreateResponse(result, HttpStatusCode.OK);
        }

        protected HttpResponseMessage CreateResponse<T>(T result, HttpStatusCode status)
        {
            var response = this.Request.CreateResponse(status, result, GlobalConfiguration.Configuration.Formatters.XmlFormatter, "application/xml");
            response.Headers.TryAddWithoutValidation("x-ms-version", "2014-02-14");
            response.Headers.TryAddWithoutValidation("x-ms-date", DateTimeOffset.UtcNow.ToString("r"));
            return response;
        }

    }
}