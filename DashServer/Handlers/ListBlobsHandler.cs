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
using System.Text.RegularExpressions;

namespace Microsoft.Dash.Server.Handlers
{
    using System;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Configuration;
    using Microsoft.WindowsAzure.Storage;

    class ListBlobsHandler : Handler
    {
        public override async Task<HttpResponseMessage> ProcessRequest(HttpRequestMessage request)
        {
            CloudStorageAccount masterAccount = CloudStorageAccount.Parse(
                ConfigurationManager.AppSettings["StorageConnectionStringMaster"]);


            string masterAccountHost = masterAccount.BlobEndpoint.Host;

            HttpRequestMessage requestKeeper = new HttpRequestMessage(request.Method, request.RequestUri);

            request.Content = null;

            HttpClient client = new HttpClient();

            HttpResponseMessage finalResponse = new HttpResponseMessage();
            HttpResponseMessage currentResponse = new HttpResponseMessage();

            request = getRequestForMasterStorageAccount(request, masterAccount);

            //we initialize finalResponse with namespace blobs and later replace each namespace blob with respective content blob info
            finalResponse = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
            string finalResponseXML = await finalResponse.Content.ReadAsStringAsync();

            //keep namespace blobs for later comparing and eventually deleting unnecessery namespace blobs
            string namespaceBlobsKeeper = finalResponseXML;

            //removes all blobs from response, to be replaced with content blobs
            finalResponseXML = removeAllBlobs(finalResponseXML);

            //reading path to the container
            XmlDocument doc = new XmlDocument();
            doc.LoadXml(finalResponseXML);
            XmlNodeList elemList = doc.GetElementsByTagName("EnumerationResults");
            string containerPath = elemList[0].Attributes["ContainerName"].Value;

            Int32 numOfAccounts = Convert.ToInt32(ConfigurationManager.AppSettings["ScaleoutNumberOfAccounts"]);

            //going through all storage accounts to include all blobs in requested container
            for (int currAccount = 0; currAccount < numOfAccounts; currAccount++)
            {
                request = new HttpRequestMessage(requestKeeper.Method, requestKeeper.RequestUri);
                request = getRequestForStorageAccount(request, currAccount, masterAccount);
                currentResponse = await client.SendAsync(request, HttpCompletionOption.ResponseContentRead);

                string currentResponseXML = await 
                    currentResponse.Content.ReadAsStringAsync();
                finalResponseXML = updateResponseContent(finalResponseXML, currentResponseXML, containerPath, masterAccountHost);
            }

            finalResponseXML = sortBlobsInXML(finalResponseXML, requestKeeper);

            //compares two responses: one is responce from virtual container, the other one is response from content blob containers
            compareTwoResponses(namespaceBlobsKeeper, finalResponseXML, requestKeeper, masterAccount);


            MemoryStream stream = new MemoryStream();
            StreamWriter writer = new StreamWriter(stream);
            writer.Write(finalResponseXML);
            writer.Flush();
            stream.Position = 0;

            finalResponse.Content = new StreamContent(stream);
            finalResponse.Content.Headers.ContentType = new MediaTypeHeaderValue("application/xml");

            return finalResponse;
        }
/*
        //reads account data and returns accountName and accountKey for currAccount (account index)
        private void readAccountData(CloudStorageAccount masterAccount, int currAccount, out string accountName, out string accountKey)
        {
            var blobClientMaster = masterAccount.CreateCloudBlobClient();
            accountName = "";
            accountKey = "";

            CloudBlobContainer masterContainer = blobClientMaster.GetContainerReference("accounts");

            CloudBlockBlob blobMaster = masterContainer.GetBlockBlobReference("accounts.txt");

            string content = blobMaster.DownloadText();

            using (StringReader sr = new StringReader(content))
            {
                //reading number of accounts
                Int32 numAcc = Convert.ToInt32(sr.ReadLine());

                //reading last account used for storing, we use hashing algorithm for now so we don't actually use this number
                Int32 x = Convert.ToInt32(sr.ReadLine());

                for (int i = 0; i <= currAccount; i++)
                {
                    accountName = sr.ReadLine();
                    accountKey = sr.ReadLine();
                }
                sr.Close();
            }

        }
*/

        private void compareTwoResponses(string namespaceBlobsKeeper, string finalResponseXML, HttpRequestMessage requestKeeper, CloudStorageAccount masterAccount)
        {

            Regex regex = new Regex("<Name>(.*?)</Name>");
            var v = regex.Matches(namespaceBlobsKeeper);

            foreach (Match m in regex.Matches(namespaceBlobsKeeper))
            {
                string blobName = m.Groups[1].ToString();
                if (!finalResponseXML.Contains(blobName))
                    deleteNamespaceBlob(blobName, requestKeeper,  masterAccount);
            }

        }

        private void deleteNamespaceBlob(string blobName, HttpRequestMessage requestKeeper, CloudStorageAccount masterAccount)
        {
            string namespaceContainerString = requestKeeper.RequestUri.AbsolutePath.Substring(1);
            CloudBlockBlob blob = GetBlobByName(masterAccount, namespaceContainerString, blobName);

            blob.Delete();
        }

        //updates finalRes which is XML formed string with currentRes
        private string removeAllBlobs(string finalResponseXML)
        {
            //mergeresponses
            XmlDocument xmlFileFinalRes = new XmlDocument();
            xmlFileFinalRes.Load(new MemoryStream(Encoding.UTF8.GetBytes(finalResponseXML)));

            xmlFileFinalRes.GetElementsByTagName("Blobs")[0].RemoveAll();

            StringWriter stringWriter = new StringWriter();
            XmlTextWriter xmlTextWriter = new XmlTextWriter(stringWriter);

            xmlFileFinalRes.WriteTo(xmlTextWriter);
            finalResponseXML = stringWriter.ToString();
            return finalResponseXML;
        }

        //updates finalRes which is XML formed string with currentRes
        private string updateResponseContent(string finalResponseXML, string currentResponseXML, string containerPath, string masterAccountHost)
        {
            //mergeresponses
            XmlDocument xmlFileFinalRes = new XmlDocument();
            xmlFileFinalRes.Load(new MemoryStream(Encoding.UTF8.GetBytes(finalResponseXML)));

            XmlDocument xmlFileCurrentRes = new XmlDocument();
            xmlFileCurrentRes.Load(new MemoryStream(Encoding.UTF8.GetBytes(currentResponseXML)));


            XmlNodeList blobList = xmlFileCurrentRes.GetElementsByTagName("Blob");


            for (int i = 0; i < blobList.Count; i++)
            {
                //
                XmlDocument doc = new XmlDocument();
                doc.LoadXml(blobList[i].OuterXml);
                XmlNode root = doc.DocumentElement;
                string blobName = root.FirstChild.InnerText;

                XmlElement elem = doc.CreateElement("Url");
                elem.InnerText = containerPath.Replace(masterAccountHost, "localhost:8080") + "/" + blobName;

                root.AppendChild(elem);

                xmlFileFinalRes.GetElementsByTagName("Blobs")[0].AppendChild(xmlFileFinalRes.ImportNode(root, true));
            }



            StringWriter stringWriter = new StringWriter();
            XmlTextWriter xmlTextWriter = new XmlTextWriter(stringWriter);

            xmlFileFinalRes.WriteTo(xmlTextWriter);
            finalResponseXML = stringWriter.ToString();
            return finalResponseXML;
        }


        //updates finalRes which is XML formed string with currentRes
        private string sortBlobsInXML(string finalRes, HttpRequestMessage request)
        {
            //mergeresponses
            XmlDocument xmlFileFinalRes = new XmlDocument();
            xmlFileFinalRes.Load(new MemoryStream(Encoding.UTF8.GetBytes(finalRes)));
            var sortedItems = xmlFileFinalRes.GetElementsByTagName("Blob").OfType<XmlElement>().OrderBy(blob => blob.FirstChild.InnerText).ToList();

            xmlFileFinalRes.GetElementsByTagName("Blobs")[0].RemoveAll();

            foreach (var item in sortedItems)
            {

                string xmlContent = item.OuterXml;
                XmlDocument doc = new XmlDocument();
                doc.LoadXml(xmlContent);
                XmlNode newNode = doc.DocumentElement;
                xmlFileFinalRes.GetElementsByTagName("Blobs")[0].AppendChild(xmlFileFinalRes.ImportNode(newNode, true));
            }

            StringWriter stringWriter = new StringWriter();
            XmlTextWriter xmlTextWriter = new XmlTextWriter(stringWriter);

            xmlFileFinalRes.WriteTo(xmlTextWriter);
            finalRes = stringWriter.ToString();
            return finalRes;
        }

        //creates a new request for storage account for ListBlob operation
        private HttpRequestMessage getRequestForStorageAccount(HttpRequestMessage request, int currAccount, CloudStorageAccount masterAccount)
        {
            string masterAccountHost = masterAccount.BlobEndpoint.Host;

            string accountName = "";
            string accountKey = "";

            readAccountData(masterAccount, currAccount, out accountName, out accountKey);


            //get container reference
            StorageCredentials credentials = new StorageCredentials(accountName, accountKey);
            CloudStorageAccount account = new CloudStorageAccount(credentials, false);
            string sas = ContainerSASFromRequest(account, request);

            //****************************************//

            request.Headers.Host = accountName + ".blob.core.windows.net";

            //creating redirection Uri
            UriBuilder forwardUri = new UriBuilder(request.RequestUri.ToString().Replace(masterAccountHost, accountName+".blob.core.windows.net") + "&" + sas.Substring(1));

            //strip off the proxy port and replace with an Http port
            forwardUri.Port = 80;
            request.RequestUri = forwardUri.Uri;

            //sets the Authorization to null, so getting the blob doesnt't use this string but sas
            request.Headers.Authorization = null;
            request.Content = null;//check if needed

            return request;
        }




        //creates a new request for storage account for ListBlob operation
        private HttpRequestMessage getRequestForMasterStorageAccount(HttpRequestMessage request, CloudStorageAccount masterAccount)
        {
            string sas = ContainerSASFromRequest(masterAccount, request);

            //creating redirection Uri
            UriBuilder forwardUri = new UriBuilder(request.RequestUri.ToString() + "&" + sas.Substring(1));

            //strip off the proxy port and replace with an Http port
            forwardUri.Port = 80;
            request.RequestUri = forwardUri.Uri;

            //sets the Authorization to null, so getting the blob doesnt't use this string but sas
            request.Headers.Authorization = null;
            request.Content = null;//check if needed

            return request;
        }
    }
}
