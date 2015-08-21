//     Copyright (c) Microsoft Corporation.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Dash.Common.Handlers;
using Microsoft.Dash.Common.Utils;
using Microsoft.Dash.Server.Utils;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;

namespace Microsoft.Dash.Server.Handlers
{
    public class BlobListHandler
    {
        public class BlobListResults
        {
            public DateTimeOffset RequestVersion { get; set; }
            public string ServiceEndpoint { get; set; }
            public string ContainerName { get; set; }
            public string Prefix { get; set; }
            public string Marker { get; set; }
            public string IndicatedMaxResults { get; set; }
            public int MaxResults { get; set; }
            public string Delimiter { get; set; }
            public IEnumerable<IListBlobItem> Blobs { get; set; }
            public string NextMarker { get; set; }
            public BlobListingDetails IncludeDetails { get; set; }
        }

        public static async Task<BlobListResults> GetBlobListing(string container, IHttpRequestWrapper request)
        {
            // Extract query parameters
            var queryParams = request.QueryParameters;
            var prefix = queryParams.Value<string>("prefix");
            var delim = queryParams.Value<string>("delimiter");
            var marker = queryParams.Value<string>("marker");
            var indicatedMaxResults = queryParams.Value<string>("maxresults");
            var maxResults = queryParams.Value("maxresults", 5000);
            var includedDataSets = String.Join(",", queryParams.Values<string>("include"));
            bool isHierarchicalListing = !String.IsNullOrWhiteSpace(delim);

            var blobTasks = DashConfiguration.DataAccounts
                .Select(account => ChildBlobListAsync(account, container, prefix, delim, includedDataSets))
                .ToArray();
            var namespaceTask = ChildBlobListAsync(DashConfiguration.NamespaceAccount, container, prefix, delim, BlobListingDetails.Metadata.ToString());
            var blobs = await Task.WhenAll(blobTasks);
            var namespaceBlobs = await namespaceTask;
            Func<string, IListBlobItem, string> makeBlobKeyWithAccount = (account, blob) => String.Format("{0}|{1}", account, blob.Uri.AbsolutePath);
            Func<IListBlobItem, string> makeBlobKey = (blob) => makeBlobKeyWithAccount(blob.Uri.Host.Substring(0, blob.Uri.Host.IndexOf('.')), blob);
            Func<NamespaceListWrapper, string> makeWrapperKey = (wrapper) => makeBlobKeyWithAccount(wrapper.IsDirectory ? String.Empty : wrapper.NamespaceBlob.PrimaryAccountName, wrapper.SourceItem);
            var sortedDataBlobs = blobs
                .SelectMany(blobList => blobList)
                .Select(blob => new {
                    Blob = blob, 
                    SortMarker = GetRawMarkerForBlob(blob),
                })
                .OrderBy(blob => blob.SortMarker, StringComparer.Ordinal)
                .SkipWhile(blob => !String.IsNullOrWhiteSpace(marker) && GetMarker(blob.SortMarker) != marker);
            // We're creating a lookup list to merge with the ns blobs. We do 2 passes here - one for actual blobs & a second for directories
            // (if applicable) as we don't have a specific account for directories - we just care they exist in the data space somewhere.
            // We also have to handle the snapshot case where there will only be 1 ns entry but multiple blobs (the current one & n snapshots)
            var dataBlobsLookup = sortedDataBlobs
                .Where(blob => blob.Blob is ICloudBlob)
                .ToLookup(blob => makeBlobKey(blob.Blob), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.Select(blob => blob.Blob), StringComparer.OrdinalIgnoreCase);
            if (isHierarchicalListing)
            {
                foreach (var directory in sortedDataBlobs
                                            .Select(blob => blob.Blob)
                                            .Where(blob => blob is CloudBlobDirectory)
                                            .ToLookup(directory => ((CloudBlobDirectory)directory).Prefix, StringComparer.OrdinalIgnoreCase))
                {
                    dataBlobsLookup[makeBlobKeyWithAccount(String.Empty, directory.First())] = Enumerable.Repeat(directory.First(), 1);
                }
            }
            var joinedList = namespaceBlobs
                .Select(nsBlob => new {
                    BlobWrapper = new NamespaceListWrapper(nsBlob),
                    SortMarker = GetRawMarkerForBlob(nsBlob),
                })
                .Where(nsBlob => nsBlob.BlobWrapper.IsDirectory || !nsBlob.BlobWrapper.NamespaceBlob.IsMarkedForDeletion)
                .OrderBy(nsBlob => nsBlob.SortMarker, StringComparer.Ordinal)
                .SkipWhile(nsBlob => !String.IsNullOrWhiteSpace(marker) && GetMarker(nsBlob.SortMarker) != marker)
                .Where(nsBlob => dataBlobsLookup.ContainsKey(makeWrapperKey(nsBlob.BlobWrapper)))
                .SelectMany(nsBlob => dataBlobsLookup[makeWrapperKey(nsBlob.BlobWrapper)]
                                                .Select(dataBlob => new {
                                                    DataBlob = dataBlob,
                                                    NamespaceWrapper = nsBlob.BlobWrapper,
                                                }));
            if (isHierarchicalListing)
            {
                // For a hierarchical listing we have to deal with a corner case in that we may have orphaned replicated data blobs
                // included in the data accounts listing as well as being included in the namespace listing. However, if there's
                // nothing but deleted blobs in the namespace for that directory, we should omit the directory.
                // This is required to mitigate our eventual consistency model for replication.
                var validDirectoryTasks = joinedList
                    .Where(blobPair => blobPair.NamespaceWrapper.IsDirectory)
                    .Select(blobPair => IsValidNamespaceDirectoryAsync((CloudBlobDirectory)blobPair.NamespaceWrapper.SourceItem))
                    .ToArray();
                var validDirectories = new HashSet<string>((await Task.WhenAll(validDirectoryTasks))
                        .Where(directory => directory.Item2)
                        .Select(directory => directory.Item1.Prefix),
                    StringComparer.OrdinalIgnoreCase);
                joinedList = joinedList
                    .Where(blobPair => !blobPair.NamespaceWrapper.IsDirectory || validDirectories.Contains(blobPair.NamespaceWrapper.Prefix));
            }
            var resultsList = joinedList
                .Take(maxResults + 1)                  // Get an extra listing so that we can generate the nextMarker
                .Select(blobPair => blobPair.DataBlob);
            return new BlobListResults
            {
                RequestVersion = request.Headers.Value("x-ms-version", StorageServiceVersions.Version_2009_09_19),
                ServiceEndpoint = request.Url.GetComponents(UriComponents.SchemeAndServer, UriFormat.Unescaped),
                ContainerName = container,
                MaxResults = maxResults,
                IndicatedMaxResults = indicatedMaxResults,
                Delimiter = delim,
                Marker = marker,
                Prefix = prefix,
                Blobs = resultsList,
                IncludeDetails = String.IsNullOrWhiteSpace(includedDataSets) ? BlobListingDetails.None : (BlobListingDetails)Enum.Parse(typeof(BlobListingDetails), includedDataSets, true),
            };
        }

        class NamespaceListWrapper
        {
            public NamespaceListWrapper(IListBlobItem srcItem)
            {
                this.SourceItem = srcItem;
                if (srcItem is CloudBlockBlob)
                {
                    this.NamespaceBlob = new NamespaceBlob((CloudBlockBlob)srcItem);
                }
            }

            public bool IsDirectory
            {
                get { return this.NamespaceBlob == null; }
            }
            public string Prefix
            {
                get
                {
                    System.Diagnostics.Debug.Assert(this.IsDirectory);
                    return ((CloudBlobDirectory)this.SourceItem).Prefix;
                }
            }

            public NamespaceBlob NamespaceBlob { get; private set; }
            public IListBlobItem SourceItem { get; private set; }
        }

        public static string GetMarkerForBlob(IListBlobItem blob)
        {
            return GetMarker(GetRawMarkerForBlob(blob));
        }

        private static string GetRawMarkerForBlob(IListBlobItem blob)
        {
            if (blob is ICloudBlob && ((ICloudBlob)blob).IsSnapshot)
            {
                return blob.Uri.AbsolutePath + '\n' + ((ICloudBlob)blob).SnapshotTime.Value.ToString("o");
            }
            else
            {
                // Append this suffix to ensure that non-snapshots are ordered AFTER snapshot blobs
                return blob.Uri.AbsolutePath + "\nzzzz";
            }
        }

        private static string GetMarker(string markerValue)
        {
            return Convert.ToBase64String(Encoding.UTF8.GetBytes(markerValue));
        }

        private static async Task<IEnumerable<IListBlobItem>> ChildBlobListAsync(CloudStorageAccount dataAccount, 
            string container, string prefix, string delimiter, string includeFlags)
        {
            CloudBlobContainer containerObj = NamespaceHandler.GetContainerByName(dataAccount, container);
            if (!String.IsNullOrWhiteSpace(delimiter))
            {
                containerObj.ServiceClient.DefaultDelimiter = delimiter;
            }
            var results = new List<IEnumerable<IListBlobItem>>();
            BlobListingDetails listDetails;
            Enum.TryParse(includeFlags, true, out listDetails);
            string nextMarker = null;
            try
            {
                do
                {
                    var continuationToken = new BlobContinuationToken
                    {
                        NextMarker = nextMarker,
                    };
                    var blobResults = await containerObj.ListBlobsSegmentedAsync(prefix, String.IsNullOrWhiteSpace(delimiter), listDetails, null, continuationToken, null, null);
                    results.Add(blobResults.Results);
                    if (blobResults.ContinuationToken != null)
                    {
                        nextMarker = blobResults.ContinuationToken.NextMarker;
                    }
                    else
                    {
                        nextMarker = null;
                    }
                } while (!String.IsNullOrWhiteSpace(nextMarker));
            }
            catch (StorageException)
            {
                // Silently swallow the exception if we're missing the container for this account

            }

            return results
                .SelectMany(segmentResults => segmentResults);
        }

        static async Task<Tuple<CloudBlobDirectory, bool>> IsValidNamespaceDirectoryAsync(CloudBlobDirectory directory)
        {
            bool result = false;
            string nextMarker = null;
            do
            {
                var continuationToken = new BlobContinuationToken
                {
                    NextMarker = nextMarker,
                };
                var blobResults = await directory.ListBlobsSegmentedAsync(true, BlobListingDetails.Metadata, null, continuationToken, null, null);
                if (blobResults.Results
                    .Select(blob => new NamespaceListWrapper(blob))
                    .Any(blobWrapper => !blobWrapper.NamespaceBlob.IsMarkedForDeletion))
                {
                    result = true;
                    break;
                }
                if (blobResults.ContinuationToken != null)
                {
                    nextMarker = blobResults.ContinuationToken.NextMarker;
                }
                else
                {
                    nextMarker = null;
                }
            } while (!String.IsNullOrWhiteSpace(nextMarker));

            return Tuple.Create(directory, result);
        }

    }
}