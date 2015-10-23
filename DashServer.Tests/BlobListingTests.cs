//     Copyright (c) Microsoft Corporation.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Xml.Linq;
using Microsoft.Dash.Common.Platform;
using Microsoft.Dash.Common.Utils;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.WindowsAzure.Storage.Blob;

namespace Microsoft.Tests
{
    [TestClass]
    public class BlobListingTests : DashTestBase
    {
        static DashTestContext _ctx;

        [ClassInitialize]
        static public void Init(TestContext ctx)
        {
            _ctx = InitializeConfigAndCreateTestBlobs(ctx, "constantx2", new Dictionary<string, string>
                {
                    { DashConfiguration.KeyWorkerQueueName, Guid.NewGuid().ToString() },
                    { "ReplicationMetadataName", ReplicateMetadataName },
                    { "LogNormalOperations", "true" }
                },
                new[] {
                    TestBlob.DefineBlob(".gitignore", copyDestination: "copyfolder/.gitignore"),
                    TestBlob.DefineBlob("Dpe Ted-Landcestry Application-O365 Azure-AAD Gateway-Gateway Development -- DLan-Gsx Ring-Movies-Graph API Test-6-26-2014-credentials.publishsettings"),
                    TestBlob.DefineBlob("ctestblob.txt"),
                    TestBlob.DefineBlob("dtestblob.txt"),
                    TestBlob.DefineBlob("etestblob.txt"),
                    TestBlob.DefineBlob("ftestblob.txt"),
                    TestBlob.DefineBlob("gtestblob.txt"),
                    TestBlob.DefineBlob("htestblob.txt"),
                    TestBlob.DefineBlob("itestblob.txt"),
                    TestBlob.DefineBlob("jtestblob.txt"),
                    TestBlob.DefineBlob("ktestblob.txt"),
                    TestBlob.DefineBlob("ltestblob.txt"),
                    TestBlob.DefineBlob("mtestblob.txt"),
                    TestBlob.DefineBlob("ntestblob.txt"),
                    TestBlob.DefineBlob("otestblob.txt"),
                    TestBlob.DefineBlob("ptestblob.txt", blobType: BlobType.PageBlob),
                    TestBlob.DefineBlob("qtestblob.txt"),
                    TestBlob.DefineBlob("rtestblob.txt"),
                    TestBlob.DefineBlob("stestblob.txt"),
                    TestBlob.DefineBlob("ttestblob.txt"),
                    TestBlob.DefineBlob("utestblob.txt"),
                    TestBlob.DefineBlob("vtestblob.txt"),
                    TestBlob.DefineBlob("wtestblob.txt"),
                    // Folder structure
                    TestBlob.DefineBlob("bfolder/testfolderblob.txt"),
                    TestBlob.DefineBlob("gfolder/testfolderblob01.txt"),
                    TestBlob.DefineBlob("gfolder/testfolderblob02.txt"),
                    TestBlob.DefineBlob("gfolder/testfolderblob03.txt", isReplicated: true),
                    TestBlob.DefineBlob("mfolder/subfolder1/testfolderblob.txt"),
                    TestBlob.DefineBlob("mfolder/subfolder2/testfolderblob01.txt"),
                    TestBlob.DefineBlob("mfolder/subfolder2/testfolderblob02.txt"),
                    // Snapshots
                    TestBlob.DefineBlob("snapshotblob.txt", numSnapshots: 2),
                    // Replicas
                    TestBlob.DefineBlob("creplicatedblob.txt", isReplicated: true),
                });
        }

        [ClassCleanup]
        public static void Cleanup()
        {
            CleanupTestBlobs(_ctx);
        }

        [TestMethod]
        public void BlobListFlatAllIncludeTest()
        {
            // The blob data includes 2 snapshots + 1 replicated blob
            var response = _ctx.Runner.ExecuteRequestWithHeaders(
                _ctx.GetContainerUri() + "&comp=list&prefix=&include=snapshots&include=uncommittedblobs&include=metadata%2Ccopy",
                "GET",
                null,
                new[] {
                    Tuple.Create("x-ms-version", "2013-08-15")
                },
                expectedStatusCode: HttpStatusCode.OK);
            var doc = XDocument.Load(response.Content.ReadAsStreamAsync().Result);
            var enumerationResults = doc.Root;
            var blobsList = enumerationResults.Element("Blobs").Elements();
            Assert.AreEqual("http://mydashserver", enumerationResults.Attribute("ServiceEndpoint").Value);
            Assert.AreEqual(_ctx.ContainerName, enumerationResults.Attribute("ContainerName").Value);
            Assert.IsNull(enumerationResults.Element("Prefix"));
            Assert.IsNull(enumerationResults.Element("Marker"));
            Assert.IsNull(enumerationResults.Element("MaxResults"));
            Assert.IsNull(enumerationResults.Element("Delimiter"));
            Assert.AreEqual(35, blobsList.Count());

            var listBlob = blobsList.First();
            Assert.AreEqual(".gitignore", listBlob.Element("Name").Value);
            Assert.IsNull(listBlob.Element("Snapshot"));
            var properties = listBlob.Element("Properties");
            Assert.AreEqual("BlockBlob", properties.Element("BlobType").Value);
            Assert.IsNotNull(properties.Element("Last-Modified").Value);
            var modifiedDate = DateTimeOffset.Parse(properties.Element("Last-Modified").Value);
            Assert.IsTrue(modifiedDate >= DateTimeOffset.UtcNow.AddMinutes(-30));
            Assert.IsNotNull(properties.Element("Etag"));
            Assert.IsTrue(!String.IsNullOrWhiteSpace(properties.Element("Etag").Value));
            Assert.AreEqual("11", properties.Element("Content-Length").Value);
            Assert.AreEqual("text/plain; charset=utf-8", properties.Element("Content-Type").Value);
            Assert.IsNotNull(properties.Element("Content-Encoding"));
            Assert.IsNotNull(properties.Element("Content-Language"));
            Assert.AreEqual("XrY7u+Ae7tCTyyK7j1rNww==", properties.Element("Content-MD5").Value);
            Assert.IsNotNull(properties.Element("Cache-Control"));
            Assert.IsNotNull(properties.Element("Content-Disposition"));
            Assert.AreEqual("unlocked", properties.Element("LeaseStatus").Value);
            Assert.AreEqual("available", properties.Element("LeaseState").Value);
            Assert.IsNull(properties.Element("CopyId"));
            Assert.IsNull(properties.Element("CopyStatus"));
            Assert.IsNull(properties.Element("CopySource"));
            Assert.IsNull(properties.Element("CopyProgress"));
            Assert.IsNull(properties.Element("CopyCompletionTime"));

            listBlob = blobsList.ElementAt(1);
            Assert.AreEqual("Dpe Ted-Landcestry Application-O365 Azure-AAD Gateway-Gateway Development -- DLan-Gsx Ring-Movies-Graph API Test-6-26-2014-credentials.publishsettings",
                            listBlob.Element("Name").Value);

            listBlob = blobsList.ElementAt(2);
            Assert.AreEqual("bfolder/testfolderblob.txt", listBlob.Element("Name").Value);

            listBlob = blobsList.ElementAt(3);
            Assert.AreEqual("copyfolder/.gitignore", listBlob.Element("Name").Value);
            properties = listBlob.Element("Properties");
            Assert.IsNotNull(properties.Element("CopyId"));
            Assert.AreEqual("success", properties.Element("CopyStatus").Value);
            // TODO: Fixup CopySource to be relative to the Dash endpoint
            Assert.IsNotNull(properties.Element("CopySource"));
            Assert.AreEqual("11/11", properties.Element("CopyProgress").Value);
            var copyCompletion = DateTimeOffset.Parse(properties.Element("CopyCompletionTime").Value);
            Assert.IsTrue(copyCompletion >= DateTimeOffset.UtcNow.AddMinutes(-30));

            listBlob = blobsList.ElementAt(4);
            Assert.AreEqual("creplicatedblob.txt", listBlob.Element("Name").Value);
            var metadata = listBlob.Element("Metadata");
            Assert.IsNotNull(metadata);
            Assert.IsNotNull(metadata.Element(ReplicateMetadataName));
            Assert.AreEqual("true", metadata.Element(ReplicateMetadataName).Value);

            listBlob = blobsList.ElementAt(9);
            Assert.AreEqual("gfolder/testfolderblob01.txt", listBlob.Element("Name").Value);
            listBlob = (XElement)listBlob.NextNode;
            Assert.AreEqual("gfolder/testfolderblob02.txt", listBlob.Element("Name").Value);
            listBlob = (XElement)listBlob.NextNode;
            Assert.AreEqual("gfolder/testfolderblob03.txt", listBlob.Element("Name").Value);

            listBlob = blobsList.ElementAt(24);
            Assert.AreEqual("ptestblob.txt", listBlob.Element("Name").Value);
            Assert.AreEqual("PageBlob", listBlob.Element("Properties").Element("BlobType").Value);

            listBlob = blobsList.ElementAt(27);
            Assert.AreEqual("snapshotblob.txt", listBlob.Element("Name").Value);
            Assert.IsNotNull(listBlob.Element("Snapshot"));
            var snapshotTime = DateTimeOffset.Parse(listBlob.Element("Snapshot").Value);
            Assert.IsTrue(snapshotTime >= DateTimeOffset.UtcNow.AddMinutes(-30));
            listBlob = (XElement)listBlob.NextNode;
            Assert.AreEqual("snapshotblob.txt", listBlob.Element("Name").Value);
            Assert.IsNotNull(listBlob.Element("Snapshot"));
            snapshotTime = DateTimeOffset.Parse(listBlob.Element("Snapshot").Value);
            Assert.IsTrue(snapshotTime >= DateTimeOffset.UtcNow.AddMinutes(-30));
            listBlob = (XElement)listBlob.NextNode;
            Assert.AreEqual("snapshotblob.txt", listBlob.Element("Name").Value);
            Assert.IsNull(listBlob.Element("Snapshot"));
        }

        [TestMethod]
        public void BlobListFlatNoIncludeTest()
        {
            var doc = _ctx.Runner.ExecuteRequestResponse(
                _ctx.GetContainerUri() + "&comp=list&prefix=",
                "GET",
                expectedStatusCode: HttpStatusCode.OK);
            var enumerationResults = doc.Root;
            Assert.AreEqual(33, enumerationResults.Element("Blobs").Elements().Count());

            var noMetadataBlob = (XElement)enumerationResults.Element("Blobs").Elements().ElementAt(4);
            Assert.AreEqual("creplicatedblob.txt", noMetadataBlob.Element("Name").Value);
            Assert.IsNull(noMetadataBlob.Element("Metadata"));

            var nonSnapshotBlob = (XElement)enumerationResults.Element("Blobs").Elements().ElementAt(27);
            Assert.AreEqual("snapshotblob.txt", nonSnapshotBlob.Element("Name").Value);
            Assert.IsNull(nonSnapshotBlob.Element("Snapshot"));
            Assert.IsNull(nonSnapshotBlob.Element("Metadata"));
        }

        [TestMethod]
        public void BlobListFlatPagedTest()
        {
            // Get the first 2 items
            var doc = _ctx.Runner.ExecuteRequestResponse(
                _ctx.GetContainerUri() + "&comp=list&prefix=&include=snapshots&maxresults=2",
                "GET");
            var enumerationResults = doc.Root;
            Assert.AreEqual(2, enumerationResults.Element("Blobs").Elements().Count());
            Assert.AreEqual("2", enumerationResults.Element("MaxResults").Value);
            Assert.IsNotNull(enumerationResults.Element("NextMarker"));

            // Get the next page that ends in the middle of listing a subdirectory
            doc = _ctx.Runner.ExecuteRequestResponse(
                _ctx.GetContainerUri() + "&comp=list&prefix=&include=snapshots&maxresults=9&marker=" + enumerationResults.Element("NextMarker").Value,
                "GET");
            enumerationResults = doc.Root;
            var blobsList = enumerationResults.Element("Blobs").Elements();
            Assert.AreEqual(9, blobsList.Count());
            var firstBlob = blobsList.First();
            Assert.AreEqual("bfolder/testfolderblob.txt", firstBlob.Element("Name").Value);
            var lastBlob = blobsList.Last();
            Assert.AreEqual("gfolder/testfolderblob02.txt", lastBlob.Element("Name").Value);

            // Get the next page that ends with the 1st of 2 snapshots
            doc = _ctx.Runner.ExecuteRequestResponse(
                _ctx.GetContainerUri() + "&comp=list&prefix=&include=snapshots&maxresults=17&marker=" + enumerationResults.Element("NextMarker").Value,
                "GET");
            enumerationResults = doc.Root;
            blobsList = enumerationResults.Element("Blobs").Elements();
            Assert.AreEqual(17, blobsList.Count());
            firstBlob = blobsList.First();
            Assert.IsNull(firstBlob.Element("Snapshot"));
            Assert.AreEqual("gfolder/testfolderblob03.txt", firstBlob.Element("Name").Value);
            lastBlob = blobsList.Last();
            Assert.AreEqual("snapshotblob.txt", lastBlob.Element("Name").Value);
            Assert.IsNotNull(lastBlob.Element("Snapshot"));
            var firstSnapshotTime = DateTimeOffset.Parse(lastBlob.Element("Snapshot").Value);

            // Get the remainer - the first item should be the next snapshot
            doc = _ctx.Runner.ExecuteRequestResponse(
                _ctx.GetContainerUri() + "&comp=list&prefix=&include=snapshots&marker=" + enumerationResults.Element("NextMarker").Value,
                "GET");
            enumerationResults = doc.Root;
            Assert.IsNull(enumerationResults.Element("NextMarker"));
            blobsList = enumerationResults.Element("Blobs").Elements();
            Assert.AreEqual(7, blobsList.Count());
            firstBlob = blobsList.First();
            Assert.AreEqual("snapshotblob.txt", firstBlob.Element("Name").Value);
            Assert.IsNotNull(firstBlob.Element("Snapshot"));
            Assert.AreNotEqual(firstSnapshotTime, DateTimeOffset.Parse(firstBlob.Element("Snapshot").Value));
        }

        [TestMethod]
        public void BlobListHierarchicalTest()
        {
            var response = _ctx.Runner.ExecuteRequestWithHeaders(
                _ctx.GetContainerUri() + "&comp=list&prefix=&delimiter=/&include=uncommittedblobs&include=metadata%2Ccopy",
                "GET",
                null,
                new[] {
                    Tuple.Create("x-ms-version", "2013-08-15")
                },
                expectedStatusCode: HttpStatusCode.OK);
            var doc = XDocument.Load(response.Content.ReadAsStreamAsync().Result);
            var enumerationResults = doc.Root;
            Assert.AreEqual("/", enumerationResults.Element("Delimiter").Value);
            var blobs = enumerationResults.Element("Blobs");
            Assert.AreEqual(29, blobs.Elements().Count());
            Assert.AreEqual(4, blobs.Elements("BlobPrefix").Count());

            var folderBlob = blobs.Elements("BlobPrefix").First();
            Assert.AreEqual("bfolder/", folderBlob.Element("Name").Value);
            Assert.AreEqual(1, folderBlob.Elements().Count());

            folderBlob = blobs.Elements("BlobPrefix").ElementAt(2);
            Assert.AreEqual("gfolder/", folderBlob.Element("Name").Value);

            folderBlob = blobs.Elements("BlobPrefix").Last();
            Assert.AreEqual("mfolder/", folderBlob.Element("Name").Value);

            // Sub-directory listing
            doc = _ctx.Runner.ExecuteRequestResponse(
                _ctx.GetContainerUri() + "&comp=list&prefix=mfolder/&delimiter=/",
                "GET");
            enumerationResults = doc.Root;
            Assert.AreEqual("mfolder/", enumerationResults.Element("Prefix").Value);
            Assert.AreEqual(2, enumerationResults.Element("Blobs").Elements().Count());
            blobs = enumerationResults.Element("Blobs");
            Assert.AreEqual(2, blobs.Elements("BlobPrefix").Count());
            string childDirPrefix = blobs.Elements().Last().Element("Name").Value;
            doc = _ctx.Runner.ExecuteRequestResponse(
                _ctx.GetContainerUri() + "&comp=list&prefix=" + childDirPrefix + "&delimiter=/",
                "GET");
            enumerationResults = doc.Root;
            Assert.AreEqual(2, enumerationResults.Element("Blobs").Elements().Count());
        }

        [TestMethod]
        public void BlobListHierarchicalPagingTest()
        {
            // Get the first 3 items
            var doc = _ctx.Runner.ExecuteRequestResponse(
                _ctx.GetContainerUri() + "&comp=list&prefix=&delimiter=/&include=metadata&maxresults=3",
                "GET");
            var enumerationResults = doc.Root;
            Assert.AreEqual(3, enumerationResults.Element("Blobs").Elements().Count());
            Assert.AreEqual("3", enumerationResults.Element("MaxResults").Value);
            Assert.IsNotNull(enumerationResults.Element("NextMarker"));
            var blobs = enumerationResults.Element("Blobs");
            Assert.AreEqual(1, blobs.Elements("BlobPrefix").Count());

            // Get the next page which should be another folder as the first item
            doc = _ctx.Runner.ExecuteRequestResponse(
                _ctx.GetContainerUri() + "&comp=list&prefix=&delimiter=/&include=metadata&marker=" + enumerationResults.Element("NextMarker").Value,
                "GET");
            enumerationResults = doc.Root;
            blobs = enumerationResults.Element("Blobs");
            Assert.AreEqual(26, blobs.Elements().Count());
            var blob = blobs.Elements().First();
            Assert.AreEqual("BlobPrefix", blob.Name);

            // Paging through a sub-directory listing
            doc = _ctx.Runner.ExecuteRequestResponse(
                _ctx.GetContainerUri() + "&comp=list&prefix=gfolder/&delimiter=/&include=metadata&maxresults=2",
                "GET");
            enumerationResults = doc.Root;
            Assert.AreEqual("gfolder/", enumerationResults.Element("Prefix").Value);
            Assert.AreEqual(2, enumerationResults.Element("Blobs").Elements().Count());
            Assert.IsNotNull(enumerationResults.Element("NextMarker"));
            blobs = enumerationResults.Element("Blobs");
            Assert.AreEqual(0, blobs.Elements("BlobPrefix").Count());
            blob = blobs.Elements().Last();
            Assert.AreEqual("gfolder/testfolderblob02.txt", blob.Element("Name").Value);
            // Next page
            doc = _ctx.Runner.ExecuteRequestResponse(
                _ctx.GetContainerUri() + "&comp=list&prefix=gfolder/&delimiter=/&include=metadata&marker=" + enumerationResults.Element("NextMarker").Value,
                "GET");
            enumerationResults = doc.Root;
            blobs = enumerationResults.Element("Blobs");
            Assert.AreEqual(1, blobs.Elements().Count());

            blob = blobs.Elements().First();
            Assert.AreEqual("gfolder/testfolderblob03.txt", blob.Element("Name").Value);
            Assert.IsNotNull(blob.Element("Metadata"));
            var metadata = blob.Element("Metadata");
            Assert.IsNotNull(metadata.Element(ReplicateMetadataName));
            Assert.AreEqual("true", metadata.Element(ReplicateMetadataName).Value);
        }

        [TestMethod]
        // TODO: Re-enable when the invalid request detection logic flows from outer branch
        [Ignore]            
        public void BlobListInvalidHierarchicalAndSnapshotTest()
        {
            // Combination of hierarchical and snapshots is invalid
            var response = _ctx.Runner.ExecuteRequestWithHeaders(
                _ctx.GetContainerUri() + "&comp=list&prefix=&delimiter=/&include=snapshots&include=metadata%2Ccopy",
                "GET",
                null,
                new[] {
                    Tuple.Create("x-ms-version", "2013-08-15")
                },
                expectedStatusCode: HttpStatusCode.BadRequest);
        }
    }
}
