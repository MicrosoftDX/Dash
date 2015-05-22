//     Copyright (c) Microsoft Corporation.  All rights reserved.

using Microsoft.Dash.Common.Handlers;
using Microsoft.Dash.Common.Utils;
using Microsoft.Dash.Server.Handlers;
using Microsoft.Dash.Server.Utils;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Web;

namespace Microsoft.Dash.Server.Authorization
{
    public class SharedAccessSignature
    {
        const string ParamResourceType      = "sr";
        const string ParamSignature         = "sig";
        const string ParamStartTime         = "st";
        const string ParamExpiryTime        = "se";
        const string ParamStoredPolicy      = "si";
        const string ParamPermissions       = "sp";
        const string ParamVersion           = "sv";
        const string ParamCacheControl      = "rscc";
        const string ParamContentDisposition= "rscd";
        const string ParamContentEncoding   = "rsce";
        const string ParamContentLang       = "rscl";
        const string ParamContentType       = "rsct";

        static readonly HashSet<string> SasParameters = new HashSet<string>(StringComparer.OrdinalIgnoreCase) {
            ParamResourceType,
            ParamSignature,
            ParamStartTime,
            ParamExpiryTime,
            ParamStoredPolicy,
            ParamPermissions,
            ParamVersion,
            ParamCacheControl,
            ParamContentDisposition,
            ParamContentEncoding,
            ParamContentLang,
            ParamContentType,
        };

        public static bool IsRequestType(RequestQueryParameters queryParams)
        {
            return queryParams.Contains(ParamResourceType) && queryParams.Contains(ParamSignature);
        }

        public static async Task<bool> IsAuthorizedAsync(IHttpRequestWrapper request, RequestHeaders headers, RequestQueryParameters queryParams, bool ignoreRequestAge)
        {
            var requestUriParts = request.UriParts;
            var resourceType = queryParams.Value<string>(ParamResourceType, String.Empty).ToLowerInvariant();
            DateTimeOffset? start = queryParams.Value(ParamStartTime, DateTimeOffset.UtcNow);
            DateTimeOffset? expiry = queryParams.Value(ParamExpiryTime, DateTimeOffset.MinValue);
            if (expiry == DateTimeOffset.MinValue)
            {
                expiry = null;
            }
            SharedAccessBlobPermissions permissions = SharedAccessBlobPolicy.PermissionsFromString(queryParams.Value(ParamPermissions, String.Empty)); 
            // Determine validity of the structure first
            if (requestUriParts.IsAccountRequest)
            {
                // SAS keys are not valid for account operations
                return false;
            }
            else if (requestUriParts.IsContainerRequest && resourceType != "c")
            {
                return false;
            }
            else if (requestUriParts.IsBlobRequest && resourceType.IndexOfAny(new [] { 'c', 'b' }) == -1)
            {
                return false;
            }
            var storedPolicyId = queryParams.Value<string>(ParamStoredPolicy);
            if (!String.IsNullOrWhiteSpace(storedPolicyId))
            {
                // Validate that we're not duplicating values for both stored access policy & url
                var storedPolicy = await GetStoredPolicyForContainer(requestUriParts.Container, storedPolicyId);
                if (storedPolicy == null)
                {
                    return false;
                }
                if (storedPolicy.SharedAccessStartTime.HasValue)
                {
                    start = storedPolicy.SharedAccessStartTime;
                }
                if (storedPolicy.SharedAccessExpiryTime.HasValue)
                {
                    if (expiry.HasValue)
                    {
                        return false;
                    }
                    expiry = storedPolicy.SharedAccessExpiryTime;
                }
                if (queryParams.Contains(ParamPermissions))
                {
                    return false;
                }
                permissions = storedPolicy.Permissions;
            }
            if (!expiry.HasValue || permissions == SharedAccessBlobPermissions.None)
            {
                return false;
            }
            else if (!ignoreRequestAge && (start.Value > DateTimeOffset.UtcNow || expiry.Value < DateTimeOffset.UtcNow))
            {
                return false;
            }
            // Verify the assigned permissions line up with the requested operation
            StorageOperationTypes requestOperation = StorageOperations.GetBlobOperation(request);
            switch (requestOperation)
            {
                case StorageOperationTypes.GetBlob:
                case StorageOperationTypes.GetBlobMetadata:
                case StorageOperationTypes.GetBlobProperties:
                case StorageOperationTypes.GetBlockList:
                case StorageOperationTypes.GetPageRanges:
                    if (!permissions.IsFlagSet(SharedAccessBlobPermissions.Read))
                    {
                        return false;
                    }
                    break;

                case StorageOperationTypes.AbortCopyBlob:
                case StorageOperationTypes.CopyBlob:
                case StorageOperationTypes.LeaseBlob:
                case StorageOperationTypes.PutBlob:
                case StorageOperationTypes.PutBlock:
                case StorageOperationTypes.PutBlockList:
                case StorageOperationTypes.PutPage:
                case StorageOperationTypes.SetBlobMetadata:
                case StorageOperationTypes.SetBlobProperties:
                case StorageOperationTypes.SnapshotBlob:
                    if (!permissions.IsFlagSet(SharedAccessBlobPermissions.Write))
                    {
                        return false;
                    }
                    break;

                case StorageOperationTypes.DeleteBlob:
                    if (!permissions.IsFlagSet(SharedAccessBlobPermissions.Delete))
                    {
                        return false;
                    }
                    break;

                case StorageOperationTypes.ListBlobs:
                    if (!permissions.IsFlagSet(SharedAccessBlobPermissions.List))
                    {
                        return false;
                    }
                    break;

                default:
                    // All other operations are not supported by SAS uris
                    return false;
            }
            DateTimeOffset sasVersion = queryParams.Value(ParamVersion, StorageServiceVersions.Version_2009_09_19);
            Func<string> stringToSignFactory = null;
            Func<string> baseStringToSign = () => String.Format("{0}\n{1}\n{2}\n{3}\n{4}",
                                                                queryParams.Value<string>(ParamPermissions),
                                                                queryParams.Value<string>(ParamStartTime),
                                                                queryParams.Value<string>(ParamExpiryTime),
                                                                GetCanonicalizedResource(requestUriParts, resourceType),
                                                                queryParams.Value<string>(ParamStoredPolicy));
            Func<string> v2012_02_12StringToSign = () => String.Format("{0}\n{1}",
                                                                        baseStringToSign(),
                                                                        queryParams.Value<string>(ParamVersion));

            if (sasVersion < StorageServiceVersions.Version_2012_02_12)
            {
                stringToSignFactory = baseStringToSign;
            }
            else if (sasVersion == StorageServiceVersions.Version_2012_02_12)
            {
                stringToSignFactory = v2012_02_12StringToSign;
            }
            else
            {
                stringToSignFactory = () => String.Format("{0}\n{1}\n{2}\n{3}\n{4}\n{5}",
                                                            v2012_02_12StringToSign(),
                                                            queryParams.Value<string>(ParamCacheControl),
                                                            queryParams.Value<string>(ParamContentDisposition),
                                                            queryParams.Value<string>(ParamContentEncoding),
                                                            queryParams.Value<string>(ParamContentLang),
                                                            queryParams.Value<string>(ParamContentType));
            }
            string signature = queryParams.Value<string>(ParamSignature);
            var usingPrimaryKey = new[] { true, false };
            int matchIndex = Array.FindIndex(usingPrimaryKey, usePrimaryKey => VerifySignature(signature, usePrimaryKey, stringToSignFactory));
            if (matchIndex != -1)
            {
                // We can't sign the redirection response when the request uses a SAS key - preserve the matching key, however
                request.AuthenticationScheme = String.Empty;
                request.AuthenticationKey = usingPrimaryKey[matchIndex] ? SharedKeySignature.PrimaryAccountKey : SharedKeySignature.SecondaryAccountKey;
                return true;
            }
            return false;
        }

        public static void RemoveSasQueryParameters(RequestQueryParameters queryParams)
        {
            // The general pattern is that a request (majority of requests will be non-SAS). Therefore, process the query params
            // in the request url until we find a SAS param. At that point switch to removing from the URL.
            foreach (var queryParam in queryParams)
            {
                if (SasParameters.Contains(queryParam.Key))
                {
                    foreach (var sasParam in SasParameters)
                    {
                        queryParams.Remove(sasParam);
                    }
                    break;
                }
            }
        }

        static string GetCanonicalizedResource(RequestUriParts uriParts, string resourceType)
        {
            if (resourceType[0] == 'c')
            {
                return String.Format("/{0}/{1}", SharedKeySignature.AccountName, uriParts.Container);
            }
            else if (resourceType[0] == 'b')
            {
                return String.Format("/{0}{1}", SharedKeySignature.AccountName, uriParts.PublicUriPath);
            }
            return String.Empty;
        }

        static bool VerifySignature(string signature, bool usePrimaryKey, Func<string> stringToSign)
        {
            if (!SharedKeySignature.HasKey(usePrimaryKey))
            {
                return false;
            }
            else if (signature == SharedKeySignature.GenerateSignature(stringToSign, usePrimaryKey))
            {
                return true;
            }
            return false;
        }

        static async Task<SharedAccessBlobPolicy> GetStoredPolicyForContainer(string container, string storedPolicyId)
        {
            try
            {
                // TODO: Move this information to cache to mitigate DoS vector
                var containerObject = NamespaceHandler.GetContainerByName(DashConfiguration.NamespaceAccount, container);
                var permissions = await containerObject.GetPermissionsAsync();
                SharedAccessBlobPolicy retval;
                if (permissions.SharedAccessPolicies.TryGetValue(storedPolicyId, out retval))
                {
                    return retval;
                }
            }
            catch (StorageException)
            {
            }
            return null;
        }

    }
}