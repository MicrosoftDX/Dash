//     Copyright (c) Microsoft Corporation.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Dash.Async;
using Microsoft.Dash.Common.Utils;
using Microsoft.WindowsAzure.Storage.Blob;

namespace Microsoft.Tests
{
    public class DashTestBase
    {
        protected const string ReplicateMetadataName = "dash_replicate_blob";

        protected class TestBlob
        {
            public static TestBlob DefineBlob(string name, bool isReplicated = false, int numSnapshots = 0, BlobType blobType = BlobType.BlockBlob, string copyDestination = "")
            {
                return new TestBlob
                {
                    Name = name,
                    IsReplicated = isReplicated,
                    NumberOfSnapshots = numSnapshots,
                    BlobType = blobType,
                    CopyDestination = copyDestination,
                };
            }

            public string Name { get; set; }
            public bool IsReplicated { get; set; }
            public int NumberOfSnapshots { get; set; }
            public BlobType BlobType { get; set; }
            public string CopyDestination { get; set; }
        }

        protected class DashTestContext
        {
            public WebApiTestRunner Runner { get; set; }
            public string ContainerName { get; set; }

            public string GetContainerUri(bool addControllerSegment = true)
            {
                return "http://mydashserver/" + (addControllerSegment ? "container/" : "") + this.ContainerName + "?restype=container";
            }

            public string GetBlobUri(string blobName, bool addControllerSegment = true)
            {
                return "http://mydashserver/" + (addControllerSegment ? "blob/" : "") + this.ContainerName + "/" + blobName;
            }

            public string GetUniqueBlobUri(bool addControllerSegment = true)
            {
                return GetBlobUri(Guid.NewGuid().ToString(), addControllerSegment);
            }
        }

        protected static DashTestContext InitializeConfigAndCreateTestBlobs(IDictionary<string, string> config, IEnumerable<TestBlob> testBlobs, string containerPrefix = "")
        {
            var ctx = InitializeConfig(config, containerPrefix);
            CreateTestBlobs(ctx, testBlobs);
            return ctx;
        }

        protected static DashTestContext InitializeConfig(IDictionary<string, string> config, string containerPrefix = "")
        {
            return new DashTestContext
            {
                Runner = new WebApiTestRunner(config),
                ContainerName = containerPrefix + Guid.NewGuid().ToString("N"),
            };
        }

        protected static void CreateTestBlobs(DashTestContext ctx, IEnumerable<TestBlob> testBlobs)
        {
            bool needAsyncRun = false;
            // Create the per-test container
            var results = ctx.Runner.ExecuteRequest(ctx.GetContainerUri(), "PUT", expectedStatusCode: HttpStatusCode.Created);
            foreach (var blobDefn in testBlobs)
            {
                HttpContent content;
                if (blobDefn.BlobType == BlobType.BlockBlob)
                {
                    content = new StringContent("hello world", System.Text.Encoding.UTF8, "text/plain");
                    content.Headers.Add("x-ms-blob-type", "BlockBlob");
                }
                else
                {
                    content = new StringContent("");
                    content.Headers.Add("x-ms-blob-type", "PageBlob");
                    content.Headers.Add("x-ms-blob-content-length", "1024");
                }
                content.Headers.Add("x-ms-version", "2013-08-15");
                if (blobDefn.IsReplicated)
                {
                    content.Headers.Add("x-ms-meta-" + ReplicateMetadataName, "true");
                    needAsyncRun = true;
                }
                // Setup correct encoding for blob names
                string blobName = ctx.GetBlobUri(PathUtils.PathEncode(blobDefn.Name));
                ctx.Runner.ExecuteRequest(blobName, "PUT", content, HttpStatusCode.Created);

                for (int snapshot = 0; snapshot < blobDefn.NumberOfSnapshots; snapshot++)
                {
                    // Pause to allow multiple snapshots to be distinguishable from one another
                    Task.Delay(1000).Wait();
                    ctx.Runner.ExecuteRequest(ctx.GetBlobUri(blobDefn.Name) + "?comp=snapshot", "PUT");
                }
                if (!String.IsNullOrWhiteSpace(blobDefn.CopyDestination))
                {
                    ctx.Runner.ExecuteRequestWithHeaders(ctx.GetBlobUri(blobDefn.CopyDestination),
                        "PUT",
                        null,
                        new[] {
                            Tuple.Create("x-ms-version", "2013-08-15"),
                            Tuple.Create("x-ms-copy-source", "http://mydashserver/" + ctx.ContainerName + "/" + blobDefn.Name),
                        },
                        HttpStatusCode.Accepted);
                }
            }
            // Allow the async stuff to run
            if (needAsyncRun)
            {
                Task.Delay(1000).Wait();
                int processed = 0, errors = 0;
                MessageProcessor.ProcessMessageLoop(ref processed, ref errors, 0);
            }
        }

        protected static void CleanupTestBlobs(DashTestContext ctx)
        {
            // Delete the container
            ctx.Runner.ExecuteRequest(ctx.GetContainerUri(), "DELETE");
        }
    }
}
