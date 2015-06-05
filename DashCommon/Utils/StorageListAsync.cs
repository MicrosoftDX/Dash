//     Copyright (c) Microsoft Corporation.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage.Blob;

namespace Microsoft.Dash.Common.Utils
{
    public static class StorageListAsync
    {
        public static async Task<bool> ListCallbackAsync(this CloudBlobContainer container, Func<IEnumerable<IListBlobItem>, Task<bool>> processBlobs)
        {
            return await ListBlobsCallbackAsync((token) => container.ListBlobsSegmentedAsync(String.Empty, true, BlobListingDetails.All, null, token, null, null), processBlobs); 
        }

        public static async Task<bool> ListBlobsCallbackAsync(Func<BlobContinuationToken, Task<BlobResultSegment>> getListing, Func<IEnumerable<IListBlobItem>, Task<bool>> processBlobs)
        {
            return await EnumerateObjectsAsync(getListing, 
                (result) => result.Results,
                (result) => result.ContinuationToken != null ? result.ContinuationToken.NextMarker : null,
                processBlobs);
        }

        public static async Task<bool> ListCallbackAsync(this CloudBlobClient client, Func<IEnumerable<CloudBlobContainer>, Task<bool>> processContainers)
        {
            return await ListContainersCallbackAsync((token) => client.ListContainersSegmentedAsync(token), processContainers);
        }

        public static async Task<bool> ListContainersCallbackAsync(Func<BlobContinuationToken, Task<ContainerResultSegment>> getListing, Func<IEnumerable<CloudBlobContainer>, Task<bool>> processContainers)
        {
            return await EnumerateObjectsAsync(getListing, 
                (result) => result.Results,
                (result) => result.ContinuationToken != null ? result.ContinuationToken.NextMarker : null,
                processContainers);
        }

        static async Task<bool> EnumerateObjectsAsync<T, Results>(Func<BlobContinuationToken, Task<Results>> getListing, 
            Func<Results, IEnumerable<T>> getResults, 
            Func<Results, string> getToken, 
            Func<IEnumerable<T>, Task<bool>> processResults)
        {
            string nextMarker = null;
            do
            {
                var continuationToken = new BlobContinuationToken
                {
                    NextMarker = nextMarker,
                };
                var results = await getListing(continuationToken);
                if (!await processResults(getResults(results)))
                {
                    return false;
                }
                nextMarker = getToken(results);
            } while (!String.IsNullOrWhiteSpace(nextMarker));

            return true;
        }
    }
}
