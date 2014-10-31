//     Copyright (c) Microsoft Corporation.  All rights reserved.

using System;
using System.Configuration;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Web;
using System.Web.Http;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Auth;
using Microsoft.WindowsAzure.Storage.Blob;

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
            return CloudStorageAccount.Parse(ConfigurationManager.AppSettings["StorageConnectionStringMaster"]);
        }

        protected CloudStorageAccount GetAccount(string accountName, string accountKey)
        {
            StorageCredentials credentials = new StorageCredentials(accountName, accountKey);
            return new CloudStorageAccount(credentials, false);
        }

        protected void ReadMetaData(CloudStorageAccount masterAccount, string origContainerName, string blobName, out Uri blobUri, out string accountName, out string accountKey, out string containerName)
        {
            CloudBlockBlob namespaceBlob = GetBlobByName(masterAccount, origContainerName, blobName);

            //Get blob metadata
            namespaceBlob.FetchAttributes();

            blobUri = new Uri(namespaceBlob.Metadata["link"]);
            accountName = namespaceBlob.Metadata["accountname"];
            accountKey = namespaceBlob.Metadata["accountkey"];
            containerName = namespaceBlob.Metadata["container"];
        }

        protected Uri GetForwardingUri(HttpRequestBase request, string accountName, string accountKey, string containerName, string blobName)
        {
            CloudStorageAccount account = GetAccount(accountName, accountKey);
            CloudBlobContainer container = GetContainerByName(account, containerName);
            CloudBlockBlob blob = GetBlobByName(account, containerName, blobName);
            string sas = calculateSASStringForContainer(container);

            //creating redirection Uri
            UriBuilder forwardUri = new UriBuilder(blob.Uri.ToString() + sas + "&" + request.Url.Query.Substring(1));
            forwardUri.Host = accountName + Endpoint();

            return forwardUri.Uri;
        }

        protected Uri GetForwardingUri(HttpRequestBase request, string accountName, string accountKey, string containerName)
        {
            CloudStorageAccount account = GetAccount(accountName, accountKey);
            CloudBlobContainer container = GetContainerByName(account, containerName);
            string sas = calculateSASStringForContainer(container);

            UriBuilder forwardUri = new UriBuilder(container.Uri.ToString() + sas + "&" + request.Url.Query.Substring(1));
            forwardUri.Host = accountName + Endpoint();

            return forwardUri.Uri;
        }

        // Not refactoring this one to use HttpRequestBase as it will be deleted once forwarding is figured out
        protected void FormForwardingRequest(Uri blobUri, string accountName, string accountKey, ref HttpRequestMessage request)
        {
            StorageCredentials credentials = new StorageCredentials(accountName, accountKey);

            CloudStorageAccount account = new CloudStorageAccount(credentials, false);

            var blobClient = account.CreateCloudBlobClient();

            string containerString = blobUri.AbsolutePath.Substring(1, blobUri.AbsolutePath.IndexOf('/', 2) - 1);

            CloudBlobContainer container = blobClient.GetContainerReference(containerString);

            string blobString = blobUri.AbsolutePath.Substring(blobUri.AbsolutePath.IndexOf('/', 2) + 1).Replace("%20", " ");

            var blob = container.GetBlockBlobReference(blobString);

            string sas = calculateSASStringForContainer(container);


            request.Headers.Host = accountName + ".blob.core.windows.net";

            //creating redirection Uri
            UriBuilder forwardUri = new UriBuilder(blob.Uri.ToString() + sas + "&" + request.RequestUri.Query.Substring(1));

            //strip off the proxy port and replace with an Http port
            forwardUri.Port = 80;

            request.RequestUri = forwardUri.Uri;

            //sets the Authorization to null, so getting the blob doesnt't use this string but sas
            request.Headers.Authorization = null;

            if (request.Method == HttpMethod.Get || request.Method == HttpMethod.Head)
            {
                request.Content = null;
            }
        }

        protected Uri GetRedirectUri(Uri blobUri, string accountName, string accountKey, string containerName, HttpRequestBase request)
        {
            StorageCredentials credentials = new StorageCredentials(accountName, accountKey);
            CloudStorageAccount account = new CloudStorageAccount(credentials, false);

            var blobClient = account.CreateCloudBlobClient();
            CloudBlobContainer container = blobClient.GetContainerReference(containerName);

            string sas = calculateSASStringForContainer(container);

            UriBuilder forwardUri;

            //creating redirection Uri
            if (request.Url.Query != "")
            {
                forwardUri = new UriBuilder(blobUri.ToString() + sas + "&" + request.Url.Query.Substring(1).Replace("timeout=90", "timeout=90000"));
            }
            else
            {
                forwardUri = new UriBuilder(blobUri.ToString() + sas);
            }

            return forwardUri.Uri;
        }

        //calculates Shared Access Signature (SAS) string based on type of request (GET, HEAD, DELETE, PUT)
        protected string calculateSASString(HttpRequestMessage request, ICloudBlob blob)
        {
            string sas = "";
            if (request.Method == HttpMethod.Get || request.Method == HttpMethod.Head)
            {
                //creating sas string (Shared Access Signature) to get permissions to access to hardcoded blob
                sas = blob.GetSharedAccessSignature(
                    new SharedAccessBlobPolicy()
                    {
                        Permissions = SharedAccessBlobPermissions.Read,
                        SharedAccessStartTime = DateTime.Now.AddMinutes(-5),
                        SharedAccessExpiryTime = DateTime.Now.AddMinutes(54)
                    });
            }
            else if (request.Method == HttpMethod.Delete)
            {
                //creating sas string (Shared Access Signature) to get permissions to access to hardcoded blob
                sas = blob.GetSharedAccessSignature(
                    new SharedAccessBlobPolicy()
                    {
                        Permissions = SharedAccessBlobPermissions.Delete,
                        SharedAccessStartTime = DateTime.Now.AddMinutes(-5),
                        SharedAccessExpiryTime = DateTime.Now.AddMinutes(54)
                    });
            }
            else if (request.Method == HttpMethod.Put)
            {
                sas = blob.GetSharedAccessSignature(
                    new SharedAccessBlobPolicy()
                    {
                        Permissions = SharedAccessBlobPermissions.Write,
                        SharedAccessStartTime = DateTime.Now.AddMinutes(-5),
                        SharedAccessExpiryTime = DateTime.Now.AddMinutes(54)
                    });
            }

            return sas;
        }

        protected async void CreateNamespaceBlob(HttpRequestBase request, CloudStorageAccount masterAccount, string container, string blob)
        {
            string accountName = "";
            string accountKey = "";

            //create an namespace blob with hardcoded metadata
            CloudBlockBlob blobMaster = GetBlobByName(masterAccount, container, blob);

            //getting storage account name and account key from file account, by using simple hashing algorithm to choose account storage
            getStorageAccount(masterAccount, blob, out accountName, out accountKey);

            if (blobMaster.Exists())
            {
                await blobMaster.FetchAttributesAsync();
            }
            else
            {
                await blobMaster.UploadTextAsync("");
            }

            blobMaster.Metadata["link"] = request.Url.Scheme + "://" + accountName + Endpoint() + container + "/" + blob;
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

            string ScaleoutAccountInfo = ConfigurationManager.AppSettings["ScaleoutStorage" + chosenAccount.ToString()];

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
            string ScaleoutAccountInfo = ConfigurationManager.AppSettings["ScaleoutStorage" + currAccount.ToString()];

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
        protected string calculateSASStringForContainer(CloudBlobContainer container)
        {
            string sas = "";

            SharedAccessBlobPolicy sasConstraints = new SharedAccessBlobPolicy();
            sasConstraints.SharedAccessExpiryTime = DateTime.UtcNow.AddHours(4);

            //we do not need all of those permisions
            sasConstraints.Permissions = SharedAccessBlobPermissions.Write | SharedAccessBlobPermissions.List | SharedAccessBlobPermissions.Read | SharedAccessBlobPermissions.Delete;

            //Generate the shared access signature on the container, setting the constraints directly on the signature.
            sas = container.GetSharedAccessSignature(sasConstraints);

            return sas;
        }

        protected Int32 NumOfAccounts()
        {
            return Convert.ToInt32(ConfigurationManager.AppSettings["ScaleoutNumberOfAccounts"]);
        }

        protected void GetNamesFromUri(Uri blobUri, out string containerName, out string blobName)
        {
            containerName = blobUri.AbsolutePath.Substring(1, blobUri.AbsolutePath.IndexOf('/', 2) - 1);
            blobName = System.IO.Path.GetFileName(blobUri.LocalPath);
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

        protected CloudBlockBlob GetBlobByUri(CloudStorageAccount masterAccount, Uri blobUri)
        {
            string namespaceContainerString = "";
            string namespaceBlobString = "";
            GetNamesFromUri(blobUri, out namespaceContainerString, out namespaceBlobString);
            return GetBlobByName(masterAccount, namespaceContainerString, namespaceBlobString);
        }

        protected CloudBlobContainer ContainerFromRequest(CloudStorageAccount account, HttpRequestBase request)
        {
            string containerString = request.Url.AbsolutePath.Substring(1);

            return GetContainerByName(account, containerString);
        }

        protected string ContainerSASFromRequest(CloudStorageAccount account, HttpRequestBase request)
        {
            CloudBlobContainer container = ContainerFromRequest(account, request);

            return calculateSASStringForContainer(container);
        }

        protected HttpRequestBase RequestFromContext(HttpContext context)
        {
            var curContext = new HttpContextWrapper(context);
            return curContext.Request;
        }
    }
}