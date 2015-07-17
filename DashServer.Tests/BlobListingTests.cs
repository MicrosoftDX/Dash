//     Copyright (c) Microsoft Corporation.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web;
using System.Web.Http;
using Microsoft.Dash.Server.Controllers;
using Microsoft.Dash.Server.Handlers;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Xml.Linq;

namespace Microsoft.Tests
{
    [TestClass]
    public class BlobListingTests
    {
        WebApiTestRunner _runner;

        [TestInitialize]
        public void Init()
        {
            _runner = new WebApiTestRunner(new Dictionary<string, string>()
                {
                    { "StorageConnectionStringMaster", "DefaultEndpointsProtocol=https;AccountName=dashtestnamespace;AccountKey=N+BMOAp/bswfqp4dxoQYLLwmYnERysm1Xxv3qSf5H9RVhQ0q+f/QKNHhXX4Z/P67mZ+5QwT6RZv9qKV834pOqQ==" },
                    { "ScaleoutStorage0", "DefaultEndpointsProtocol=https;AccountName=dashtestconstant1;AccountKey=Q32sd0MkbWpMfBb0A3Zjg8LhSI0VJblT+CyAbXkczI2rWNYIrsoaQjc7ba1z5w+KOpJtxl/h3vA20WsbENM6hQ==" },
                    { "ScaleoutStorage1", "DefaultEndpointsProtocol=https;AccountName=dashtestconstant2;AccountKey=DHkdb1s/P0K0bDGJ5CaAGjN9HTv7UL1mZ9nriYn0bOkeX0V9qVaDqVp3RjPoJ6CnKarzhGGd4+H84D+ureNisA==" },
                    { "ScaleoutNumberOfAccounts", "2"},
                });
        }

        [TestMethod]
        public void BlobListFlatAllIncludeTest()
        {
            // The blob data includes 2 snapshots + 1 replicated blob
            var response = _runner.ExecuteRequestWithHeaders(
                "http://mydashserver/container/test?restype=container&comp=list&prefix=&include=snapshots&include=uncommittedblobs&include=metadata%2Ccopy",
                "GET",
                null,
                new [] {
                    Tuple.Create("x-ms-version", "2013-08-15")
                },
                expectedStatusCode: HttpStatusCode.OK);
            var doc = XDocument.Load(response.Content.ReadAsStreamAsync().Result);
            var enumerationResults = doc.Root;
            Assert.AreEqual("http://mydashserver", enumerationResults.Attribute("ServiceEndpoint").Value);
            Assert.AreEqual("test", enumerationResults.Attribute("ContainerName").Value);
            Assert.IsNull(enumerationResults.Element("Prefix"));
            Assert.IsNull(enumerationResults.Element("Marker"));
            Assert.IsNull(enumerationResults.Element("MaxResults"));
            Assert.IsNull(enumerationResults.Element("Delimiter"));
            Assert.AreEqual(22, enumerationResults.Element("Blobs").Elements().Count());
            var firstBlob = (XElement)enumerationResults.Element("Blobs").FirstNode;
            Assert.AreEqual(".gitignore", firstBlob.Element("Name").Value);
            Assert.IsNotNull(firstBlob.Element("Snapshot").Value);
            Assert.IsNull(((XElement)firstBlob.NextNode).Element("Snapshot"));
            var replicatedBlob = (XElement)enumerationResults.Element("Blobs").Elements().Skip(2).First();
            Assert.AreEqual("DataAtScaleHub.sln", replicatedBlob.Element("Name").Value);
            Assert.AreEqual("Dpe Ted-Landcestry Application-O365 Azure-AAD Gateway-Gateway Development -- DLan-Gsx Ring-Movies-Graph API Test-6-26-2014-credentials.publishsettings", 
                ((XElement)replicatedBlob.NextNode).Element("Name").Value);
            var blobInSubDir = (XElement)enumerationResults.Element("Blobs").Elements().Skip(6).First();
            Assert.AreEqual("Package/Console/Package/UpdatePackage/console.zip", blobInSubDir.Element("Name").Value);
            Assert.AreEqual("application/x-zip-compressed", blobInSubDir.Descendants("Content-Type").First().Value);
            Assert.AreEqual("", blobInSubDir.Descendants("Content-Encoding").First().Value);
            var snapshotBlob = (XElement)enumerationResults.Element("Blobs").Elements().Skip(20).First();
            Assert.IsNotNull(snapshotBlob.Element("Snapshot").Value);
            Assert.IsNotNull(snapshotBlob.Element("Metadata"));
            Assert.IsNotNull(snapshotBlob.Descendants("fred"));
        }

        [TestMethod]
        public void BlobListFlatNoIncludeTest()
        {
            var doc = _runner.ExecuteRequestResponse(
                "http://mydashserver/container/test?restype=container&comp=list&prefix=",
                "GET",
                expectedStatusCode: HttpStatusCode.OK);
            var enumerationResults = doc.Root;
            Assert.AreEqual(20, enumerationResults.Element("Blobs").Elements().Count());
            var nonSnapshotBlob = (XElement)enumerationResults.Element("Blobs").Elements().Skip(19).First();
            Assert.IsNull(nonSnapshotBlob.Element("Snapshot"));
            Assert.IsNull(nonSnapshotBlob.Element("Metadata"));
        }

        [TestMethod]
        public void BlobListFlatPagedTest()
        {
            var doc = _runner.ExecuteRequestResponse(
                "http://mydashserver/container/test?restype=container&comp=list&prefix=&include=snapshots&maxresults=2",
                "GET");
            var enumerationResults = doc.Root;
            Assert.AreEqual(2, enumerationResults.Element("Blobs").Elements().Count());
            Assert.AreEqual("2", enumerationResults.Element("MaxResults").Value);
            Assert.IsNotNull(enumerationResults.Element("NextMarker"));

            doc = _runner.ExecuteRequestResponse(
                "http://mydashserver/container/test?restype=container&comp=list&prefix=&include=snapshots&maxresults=19&marker=" + enumerationResults.Element("NextMarker").Value,
                "GET");
            enumerationResults = doc.Root;
            Assert.AreEqual(19, enumerationResults.Element("Blobs").Elements().Count());
            var firstBlob = (XElement)enumerationResults.Element("Blobs").FirstNode;
            Assert.AreEqual("DataAtScaleHub.sln", firstBlob.Element("Name").Value);
            var lastBlob = (XElement)enumerationResults.Element("Blobs").LastNode;
            Assert.IsNotNull(lastBlob.Element("Snapshot").Value);
            Assert.AreEqual("test.txt", lastBlob.Element("Name").Value);

            doc = _runner.ExecuteRequestResponse(
                "http://mydashserver/container/test?restype=container&comp=list&prefix=&include=snapshots&marker=" + enumerationResults.Element("NextMarker").Value,
                "GET");
            enumerationResults = doc.Root;
            Assert.IsNull(enumerationResults.Element("NextMarker"));
            firstBlob = (XElement)enumerationResults.Element("Blobs").FirstNode;
            Assert.AreEqual("test.txt", firstBlob.Element("Name").Value);
            Assert.IsNull(firstBlob.Element("Snapshot"));
        }

        [TestMethod]
        public void BlobListHierarchicalTest()
        {
            var doc = _runner.ExecuteRequestResponse(
                "http://mydashserver/container/test?restype=container&comp=list&prefix=&delimiter=/&include=uncommittedblobs&include=metadata%2Ccopy",
                "GET");
            var enumerationResults = doc.Root;
            Assert.AreEqual("/", enumerationResults.Element("Delimiter").Value);
            var blobs = enumerationResults.Element("Blobs");
            Assert.AreEqual(12, blobs.Elements().Count());
            var directory = blobs.Element("BlobPrefix");
            Assert.IsNotNull(directory);
            Assert.AreEqual("Package/", directory.Element("Name").Value);
        }

    }
}
