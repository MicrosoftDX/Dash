//     Copyright (c) Microsoft Corporation.  All rights reserved.

using System.IO;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Microsoft.WindowsAzure.Storage.Auth;
using Microsoft.WindowsAzure.Storage.Blob;
using System.Web;
using System.Xml;
using System.Xml.Linq;
using System.Linq;
using System.Data;

namespace Microsoft.Dash.Server.Handlers
{
    using System;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Configuration;
    using Microsoft.WindowsAzure.Storage;

    class CopyBlobHandler : Handler
    {
        //copies namespace blob within the same master account and between (possibly) different containers
        //copies content blob within the same storage account and between (possibly) different containers in it
        public override async Task<HttpResponseMessage> ProcessRequest(HttpRequestMessage request)
        {
            CloudStorageAccount masterAccount = CloudStorageAccount.Parse(
                ConfigurationManager.AppSettings["StorageConnectionStringMaster"]);

            string masterAccountHost = masterAccount.BlobEndpoint.Host;

            String accountName = "";
            String accountKey = "";

            //changinh copy sorurce Header
            string copySource = request.Headers.GetValues("x-ms-copy-source").First().Replace("localhost:8080", masterAccountHost);

            request.Headers.Remove("x-ms-copy-source");
            request.Headers.Add("x-ms-copy-source", copySource);

            Uri copySourceUri = new Uri(copySource);

            string sourceContainerName = copySourceUri.AbsolutePath.Substring(1, copySourceUri.AbsolutePath.IndexOf('/', 2) - 1);
            string newContainerName = request.RequestUri.AbsolutePath.Substring(1, request.RequestUri.AbsolutePath.IndexOf('/', 2) - 1);
            string sourceBlobName = copySourceUri.AbsolutePath.Substring(copySourceUri.AbsolutePath.IndexOf('/', 2) + 1);
            string newBlobName = request.RequestUri.AbsolutePath.Substring(request.RequestUri.AbsolutePath.IndexOf('/', 2) + 1);

            //reading metadata from source blob
            ReadMetaDataFromSource(copySourceUri, masterAccount, out accountName, out accountKey);

            //if we copy blob to different storage account we will have to have two calls to read meta data to get two different credentials

            StorageCredentials credentials = new StorageCredentials(accountName, accountKey);
            CloudStorageAccount account = new CloudStorageAccount(credentials, false);

            // Create the source content blob
            CloudBlockBlob sourceBlockBlob = GetBlobByName(account, sourceContainerName, sourceBlobName);

            // Create the new content blob
            CloudBlockBlob newBlockBlob = GetBlobByName(account, newContainerName, newBlobName);

            //copies content blobs
            await newBlockBlob.StartCopyFromBlobAsync(sourceBlockBlob, null, null, null, null);

            HttpClient client = new HttpClient();

            HttpResponseMessage response = new HttpResponseMessage();

            string newLink = request.RequestUri.Scheme + "://" + masterAccountHost + "/" + newContainerName + "/" + newBlobName;

            CloudBlockBlob newNamespaceBlockBlob = GetBlobByName(masterAccount, newContainerName, newBlobName);

            // Manually getting the container and blob because we need a reference to the container below.
            CloudBlobClient sourceNamespaceBlobClient = masterAccount.CreateCloudBlobClient();
            CloudBlobContainer sourceNamespaceBlobContainer = sourceNamespaceBlobClient.GetContainerReference(sourceContainerName);
            CloudBlockBlob sourceNamespaceBlockBlob = sourceNamespaceBlobContainer.GetBlockBlobReference(sourceBlobName);

            //creating sas string for namespace container because authorization doesn't work anymore because we changed request headers explicitly in proxyhandler
            string sas = base.calculateSASStringForContainer(sourceNamespaceBlobContainer);
            request.RequestUri = new Uri(newLink + sas + "&" + request.RequestUri.Query.Substring(1));
            request.Headers.Authorization = null;

            //changing copy sorurce Header
            copySource = request.Headers.GetValues("x-ms-copy-source").First().Substring(0, request.Headers.GetValues("x-ms-copy-source").First().IndexOf("?"))+sas;

            request.Headers.Remove("x-ms-copy-source");
            request.Headers.Add("x-ms-copy-source", copySource);

            response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);

            //updating metadata of new Namespace blob
            newNamespaceBlockBlob.FetchAttributes();
            newNamespaceBlockBlob.Metadata["link"] = newLink.Replace(masterAccountHost, accountName);
            newNamespaceBlockBlob.Metadata["container"] = newContainerName;
            newNamespaceBlockBlob.Metadata["blobname"] = newBlobName;
            newNamespaceBlockBlob.SetMetadata();

            return response;
        }


        //getting storage account name and account key from metadata of a source file
        //passing argument sourceUri because we want our copied file to be on the same account storage (same hash code)
        //if we want to chooze by random the storage account on which we want to copy blob than we should have two storage credentials
        protected void ReadMetaDataFromSource(Uri sourceUri, CloudStorageAccount masterAccount, out String accountName, out String accountKey)
        {
            string blobName = System.IO.Path.GetFileName(sourceUri.LocalPath);
            string containerName = sourceUri.AbsolutePath.Substring(1, sourceUri.AbsolutePath.IndexOf('/', 2) - 1);
            CloudBlockBlob namespaceBlob = GetBlobByName(masterAccount, containerName, blobName);

            //Get blob metadata
            namespaceBlob.FetchAttributes();
            accountName = namespaceBlob.Metadata["accountname"];
            accountKey = namespaceBlob.Metadata["accountkey"];
        }
    }
}
