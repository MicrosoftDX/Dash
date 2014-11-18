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
    public class ContainerTests
    {
        WebApiTestRunner _runner;

        [TestInitialize]
        public void Init()
        {
            _runner = new WebApiTestRunner(new Dictionary<string, string>()
                {
                    { "StorageConnectionStringMaster", "DefaultEndpointsProtocol=http;AccountName=dashstorage0;AccountKey=uCNvIdXcltACBiDUMyO0BflZpKmjseplqOlzE62tx87qnkwpUMBV/GQhrscW9lmdZVT0x8DilYqUoHMNBlVIGg==" },
                    { "ScaleoutStorage0", "DefaultEndpointsProtocol=http;AccountName=dashstorage1;AccountKey=8jqRVtXUWiEthgIhR+dFwrB8gh3lFuquvJQ1v4eabObIj7okI1cZIuzY8zZHmEdpcC0f+XlUkbFwAhjTfyrLIg==" },
                    { "ScaleoutNumberOfAccounts", "1"},
                });
        }

        [TestMethod]
        public void BlobListFlatAllIncludeTest()
        {
            var doc = _runner.ExecuteRequestResponse(
                "http://mydashserver/container/test?restype=container&comp=list&prefix=&include=snapshots&include=uncommittedblobs&include=metadata%2Ccopy", 
                "GET",
                expectedStatusCode: HttpStatusCode.OK);
            var enumerationResults = doc.Root;
            Assert.AreEqual("http://mydashserver", enumerationResults.Attribute("ServiceEndpoint").Value);
            Assert.AreEqual("test", enumerationResults.Attribute("ContainerName").Value);
            Assert.IsNull(enumerationResults.Element("Prefix"));
            Assert.IsNull(enumerationResults.Element("Marker"));
            Assert.IsNull(enumerationResults.Element("MaxResults"));
            Assert.IsNull(enumerationResults.Element("Delimiter"));
            Assert.AreEqual(21, enumerationResults.Element("Blobs").Elements().Count());
            var firstBlob = (XElement)enumerationResults.Element("Blobs").FirstNode;
            Assert.AreEqual(".gitignore", firstBlob.Element("Name").Value);
            var blobInSubDir = (XElement)enumerationResults.Element("Blobs").Elements().Skip(5).First();
            Assert.AreEqual("Package/Console/Package/UpdatePackage/console.zip", blobInSubDir.Element("Name").Value);
            Assert.AreEqual("application/x-zip-compressed", blobInSubDir.Descendants("Content-Type").First().Value);
            Assert.AreEqual("", blobInSubDir.Descendants("Content-Encoding").First().Value);
            var snapshotBlob = (XElement)enumerationResults.Element("Blobs").Elements().Skip(19).First();
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
                "http://mydashserver/container/test?restype=container&comp=list&prefix=&include=snapshots&maxresults=18&marker=" + enumerationResults.Element("NextMarker").Value, 
                "GET");
            enumerationResults = doc.Root;
            Assert.AreEqual(18, enumerationResults.Element("Blobs").Elements().Count());
            var firstBlob = (XElement)enumerationResults.Element("Blobs").FirstNode;
            Assert.AreEqual("Dpe Ted-Landcestry Application-O365 Azure-AAD Gateway-Gateway Development -- DLan-Gsx Ring-Movies-Graph API Test-6-26-2014-credentials.publishsettings", firstBlob.Element("Name").Value);
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
            Assert.AreEqual(11, blobs.Elements().Count());
            var directory = blobs.Element("BlobPrefix");
            Assert.IsNotNull(directory);
            Assert.AreEqual("Package/", directory.Element("Name").Value);
        }

        [TestMethod]
        public void ContainerLifecycleTest()
        {
            string containerName = Guid.NewGuid().ToString("N");
            string baseUri = "http://mydashserver/container/" + containerName + "?restype=container";
            var results = _runner.ExecuteRequest(baseUri, "PUT", expectedStatusCode: HttpStatusCode.Created);

            //Try to re-create the same container again.
            results = _runner.ExecuteRequest(baseUri, "PUT", expectedStatusCode: HttpStatusCode.Conflict);
            //TODO: Add more variations on create container, including attempt to create already existing container

            var content = new StringContent("", System.Text.Encoding.UTF8, "application/xml");
            content.Headers.Add("x-ms-meta-foo", "fee");
            content.Headers.Add("x-ms-meta-Dog", "Cat");
            results = _runner.ExecuteRequest(baseUri + "&comp=metadata", "PUT");
            //Assert.AreEqual(HttpStatusCode.OK, results.StatusCode, "Expected OK result");

            results = _runner.ExecuteRequest(baseUri, "DELETE", expectedStatusCode: HttpStatusCode.Accepted);
        }

        [TestMethod]
        public void DeleteNonExistentContainerTest()
        {
            string containerName = Guid.NewGuid().ToString("N");
            string baseUri = "http://mydashserver/container/" + containerName + "?restype=container";
            var results = _runner.ExecuteRequest(baseUri, "DELETE", expectedStatusCode: HttpStatusCode.NotFound);
        }

    }
}
