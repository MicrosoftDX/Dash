﻿//     Copyright (c) Microsoft Corporation.  All rights reserved.

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

namespace Microsoft.Dash.Server.Controllers
{
    public class CommonController : ApiController
    {
        protected string Endpoint()
        {
            return ".blob.core.windows.net";
        }
        protected CloudStorageAccount GetMasterAccount()
        {
            return CloudStorageAccount.Parse(AzureUtils.GetConfigSetting("StorageConnectionStringMaster", ""));
        }

        protected CloudStorageAccount GetAccount(string accountName, string accountKey)
        {
            StorageCredentials credentials = new StorageCredentials(accountName, accountKey);
            return new CloudStorageAccount(credentials, false);
        }

        protected void ReadMetaData(CloudStorageAccount masterAccount, string origContainerName, string origBlobName, out string accountName, out string accountKey, out string containerName, out string blobName)
        {
            CloudBlockBlob namespaceBlob = GetBlobByName(masterAccount, origContainerName, origBlobName);

            //Get blob metadata
            namespaceBlob.FetchAttributes();
            accountName = namespaceBlob.Metadata["accountname"];
            accountKey = namespaceBlob.Metadata["accountkey"];
            containerName = namespaceBlob.Metadata["container"];
            blobName = namespaceBlob.Metadata["blobname"];
        }

        protected Uri GetForwardingUri(HttpRequestBase request, string accountName, string accountKey, string containerName)
        {
            CloudStorageAccount account = GetAccount(accountName, accountKey);
            CloudBlobContainer container = GetContainerByName(account, containerName);

            UriBuilder forwardUri = new UriBuilder()
            {
                Scheme = container.Uri.Scheme,
                Host = accountName + Endpoint(),
                Path = container.Uri.AbsolutePath,
                Query = request.Url.Query
            };

            return forwardUri.Uri;
        }

        protected Uri GetRedirectUri(HttpRequestBase request, string accountName, string accountKey, string containerName, string blobName)
        {
            CloudStorageAccount account = GetAccount(accountName, accountKey);
            CloudBlobContainer container = GetContainerByName(account, containerName);

            string sas = calculateSASStringForContainer(request, container);

            UriBuilder forwardUri = new UriBuilder()
            {
                Scheme = request.Url.Scheme,
                Host = accountName + Endpoint(),
                Path = containerName + "/" + blobName,
                Query = sas
            };

            //creating redirection Uri
            if (!string.IsNullOrWhiteSpace(request.Url.Query))
            {
                forwardUri.Query += "&" + request.Url.Query.Substring(1).Replace("timeout=90", "timeout=90000");
            }

            return forwardUri.Uri;
        }

        protected Uri ForwardUriToNamespace(HttpRequestBase request)
        {
            CloudStorageAccount masterAccount = GetMasterAccount();
            UriBuilder forwardUri = new UriBuilder()
            {
                Scheme = request.Url.Scheme,
                Host = masterAccount.Credentials.AccountName + Endpoint(),
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

        protected async void CreateNamespaceBlob(HttpRequestBase request, CloudStorageAccount masterAccount, string container, string blob)
        {
            string accountName = "";
            string accountKey = "";

            //create an namespace blob with hardcoded metadata
            CloudBlockBlob blobMaster = GetBlobByName(masterAccount, container, blob);

            if (blobMaster.Exists())
            {
                await blobMaster.FetchAttributesAsync();
                //If we already have a blob, the rest of the metadata is there, too. Just return.
                if (!string.IsNullOrWhiteSpace(blobMaster.Metadata["blobname"]))
                {
                    return;
                }
            }
            else
            {
                await blobMaster.UploadTextAsync("");
            }

            //getting storage account name and account key from file account, by using simple hashing algorithm to choose account storage
            getStorageAccount(masterAccount, blob, out accountName, out accountKey);
            blobMaster.Metadata["accountname"] = accountName;
            blobMaster.Metadata["accountkey"] = accountKey;
            blobMaster.Metadata["container"] = container;
            blobMaster.Metadata["blobname"] = blob;
            await blobMaster.SetMetadataAsync();
        }

        //getting storage account name and account key from file account, by using simple hashing algorithm to choose account storage
        protected void getStorageAccount(CloudStorageAccount masterAccount, string masterBlobString, out string accountName, out string accountKey)
        {
            int numAcc = NumOfAccounts();
            long chosenAccount = GetInt64HashCode(masterBlobString, numAcc);
            string ScaleoutAccountInfo = AzureUtils.GetConfigSetting("ScaleoutStorage" + chosenAccount.ToString(), "");

            //getting account name
            Match match1 = Regex.Match(ScaleoutAccountInfo, @"AccountName=([A-Za-z0-9\-]+);", RegexOptions.IgnoreCase);
            accountName = "";
            if (match1.Success)
            {
                accountName = match1.Groups[1].Value;
            }

            //getting account key
            accountKey = ScaleoutAccountInfo.Substring(ScaleoutAccountInfo.IndexOf("AccountKey=") + 11);
        }



        /// <summary>
        /// Return unique Int64 value for input string
        /// </summary>
        /// <param name="strText"></param>
        /// <returns></returns>
        static Int64 GetInt64HashCode(string strText, int numAcc)
        {
            long hashCode = 0;
            if (!string.IsNullOrEmpty(strText))
            {
                byte[] byteContents = Encoding.UTF8.GetBytes(strText);
                System.Security.Cryptography.SHA256 hash =
                new System.Security.Cryptography.SHA256CryptoServiceProvider();
                byte[] hashText = hash.ComputeHash(byteContents);
                long hashCodeStart = BitConverter.ToInt64(hashText, 0);
                long hashCodeMedium = BitConverter.ToInt64(hashText, 8);
                long hashCodeEnd = BitConverter.ToInt64(hashText, 24);
                hashCode = hashCodeStart ^ hashCodeMedium ^ hashCodeEnd;
            }
            return (hashCode > 0) ? hashCode % numAcc : (-hashCode) % numAcc;
        }



        //getting storage account name and account key from file account, by using simple round-robin algorithm to choose account storage, NOT USED AT THE MOMENT
        private void getStorageAccountRoundRobin(CloudStorageAccount masterAccount, out string accountName, out string accountKey)
        {
            var blobClientMaster = masterAccount.CreateCloudBlobClient();
            accountName = "";
            accountKey = "";

            CloudBlobContainer masterContainer = blobClientMaster.GetContainerReference("accounts");

            CloudBlockBlob blobMaster = masterContainer.GetBlockBlobReference("accounts.txt");

            string content = blobMaster.DownloadText();
            StringBuilder newContent = new StringBuilder();

            using (StringReader sr = new StringReader(content))
            using (StringWriter sw = new StringWriter(newContent))
            {
                //reading number of accounts
                int numAcc = Convert.ToInt32(sr.ReadLine());

                //reading last account used for storing, we use round-robin algorithm so next account is curAcc+1 (mod numAcc)
                int curAcc = Convert.ToInt32(sr.ReadLine());

                //calculating next storage account for storing
                curAcc = curAcc + 1;
                if (curAcc > numAcc)
                {
                    curAcc = 1;
                }

                sw.WriteLine(numAcc.ToString() + "\r\n" + curAcc.ToString());
                string temp;
                for (int i = 1; i <= numAcc; i++)
                {
                    temp = sr.ReadLine();
                    sw.WriteLine(temp);

                    if (i == curAcc)
                    {
                        accountName = temp;
                    }

                    temp = sr.ReadLine();
                    sw.WriteLine(temp);

                    if (i == curAcc)
                    {
                        accountKey = temp;
                    }
                }

                sw.Close();
                sr.Close();

            }
            blobMaster.UploadText(newContent.ToString());

        }

        //reads account data and returns accountName and accountKey for currAccount (account index)
        protected void readAccountData(CloudStorageAccount masterAccount, int currAccount, out string accountName, out string accountKey)
        {
            string ScaleoutAccountInfo = AzureUtils.GetConfigSetting("ScaleoutStorage" + currAccount.ToString(), "");

            //getting account name
            Match match1 = Regex.Match(ScaleoutAccountInfo, @"AccountName=([A-Za-z0-9\-]+);", RegexOptions.IgnoreCase);
            accountName = "";
            if (match1.Success)
            {
                accountName = match1.Groups[1].Value;
            }

            //getting account key
            accountKey = ScaleoutAccountInfo.Substring(ScaleoutAccountInfo.IndexOf("AccountKey=") + 11);
        }

        //calculates SAS string to have access to a container
        protected string calculateSASStringForContainer(HttpRequestBase request, CloudBlobContainer container)
        {
            SharedAccessBlobPolicy sasConstraints = GetSasPolicy(request);
            //Generate the shared access signature on the container, setting the constraints directly on the signature.
            return container.GetSharedAccessSignature(sasConstraints);
        }

        protected Int32 NumOfAccounts()
        {
            return AzureUtils.GetConfigSetting("ScaleoutNumberOfAccounts", 0);
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