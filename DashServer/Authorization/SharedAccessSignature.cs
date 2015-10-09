//     Copyright (c) Microsoft Corporation.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Microsoft.Dash.Common.Handlers;
using Microsoft.Dash.Common.Utils;
using Microsoft.Dash.Server.Handlers;
using Microsoft.Dash.Server.Utils;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;

namespace Microsoft.Dash.Server.Authorization
{
    public class SharedAccessSignature
    {
        const string ParamResourceType      = "sr";
        const string ParamResourceTypeEx    = "srt";
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
        const string ParamServices          = "ss";
        const string ParamSourceIP          = "sip";
        const string ParamProtocol          = "spr";

        static readonly HashSet<string> SasParameters = new HashSet<string>(StringComparer.OrdinalIgnoreCase) {
            ParamResourceType,
            ParamResourceTypeEx,
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
            ParamServices,
            ParamSourceIP,
            ParamProtocol,
        };

        public static bool IsRequestType(RequestQueryParameters queryParams)
        {
            return queryParams.Contains(ParamSignature) && (queryParams.Contains(ParamResourceType) || queryParams.Contains(ParamResourceTypeEx));
        }

        public static async Task<bool> IsAuthorizedAsync(IHttpRequestWrapper request, RequestHeaders headers, RequestQueryParameters queryParams, bool ignoreRequestAge)
        {
            var requestUriParts = request.UriParts;
            string resourceType = String.Empty;
            DateTimeOffset? start = queryParams.Value(ParamStartTime, DateTimeOffset.UtcNow);
            DateTimeOffset? expiry = queryParams.Value(ParamExpiryTime, DateTimeOffset.MinValue);
            DateTimeOffset sasVersion = queryParams.Value(ParamVersion, StorageServiceVersions.Version_2009_09_19);
            bool accountSas = queryParams.Contains(ParamResourceTypeEx);
            if (accountSas)
            {
                resourceType = queryParams.Value<string>(ParamResourceTypeEx).ToLowerInvariant();
            }
            else
            {
                resourceType = queryParams.Value<string>(ParamResourceType).ToLowerInvariant();
            }
            if (expiry == DateTimeOffset.MinValue)
            {
                expiry = null;
            }
            SharedAccessBlobPermissions permissions = PermissionsFromString(queryParams.Value(ParamPermissions, String.Empty)); 
            // Determine validity of the structure first
            if (requestUriParts.IsAccountRequest)
            {
                if (sasVersion < StorageServiceVersions.Version_2015_04_05 || !resourceType.Contains('s'))
                {
                    // SAS keys are not valid for account operations before 2015-04-05
                    return false;
                }
            }
            else if (requestUriParts.IsContainerRequest)
            {
                if (resourceType != "c")
                {
                    return false;
                }
            }
            else if (requestUriParts.IsBlobRequest)
            {
                if (resourceType.IndexOfAny(new[] { 'c', 'b', 'o' }) == -1)
                {
                    return false;
                }
            }
            if (!accountSas)
            {
                var storedPolicyId = queryParams.Value<string>(ParamStoredPolicy);
                if (!String.IsNullOrWhiteSpace(storedPolicyId))
                {
                    // Validate that we're not duplicating values for both stored access policy & url
                    // Allow stored policy to the specified from a different container for test purposes - this isn't a security violation as it must come from the same account.
                    var aclContainer = headers.Value("StoredPolicyContainer", String.Empty);
                    if (String.IsNullOrEmpty(aclContainer))
                    {
                        aclContainer = requestUriParts.Container;
                    }
                    var storedPolicy = await GetStoredPolicyForContainer(aclContainer, storedPolicyId);
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
            }
            else
            {
                if (!queryParams.Value<string>(ParamServices, String.Empty).Contains('b'))
                {
                    // SAS must include blob service
                    return false;
                }
            }
            if (!expiry.HasValue || permissions == SharedAccessBlobPermissions.None)
            {
                return false;
            }
            else if (!ignoreRequestAge && (start.Value > DateTimeOffset.UtcNow || expiry.Value < DateTimeOffset.UtcNow))
            {
                return false;
            }
            else if (!IsProtocolMatched(request, queryParams.Value<string>(ParamProtocol)))
            {
                return false;
            }
            else if (!IsSourceAddressInRange(request, queryParams.Value<string>(ParamSourceIP)))
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
                case StorageOperationTypes.GetBlobServiceProperties:
                case StorageOperationTypes.GetBlobServiceStats:
                case StorageOperationTypes.GetContainerProperties:
                case StorageOperationTypes.GetContainerMetadata:
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
                case StorageOperationTypes.SetBlobServiceProperties:
                case StorageOperationTypes.CreateContainer:
                case StorageOperationTypes.SetContainerMetadata:
                case StorageOperationTypes.LeaseContainer:
                    if (!permissions.IsFlagSet(SharedAccessBlobPermissions.Write))
                    {
                        return false;
                    }
                    break;

                case StorageOperationTypes.DeleteBlob:
                case StorageOperationTypes.DeleteContainer:
                    if (!permissions.IsFlagSet(SharedAccessBlobPermissions.Delete))
                    {
                        return false;
                    }
                    break;

                case StorageOperationTypes.ListBlobs:
                case StorageOperationTypes.ListContainers:
                    if (!permissions.IsFlagSet(SharedAccessBlobPermissions.List))
                    {
                        return false;
                    }
                    break;

                default:
                    // All other operations are not supported by SAS uris
                    return false;
            }
            Func<string> stringToSignFactory = null;
            if (!accountSas)
            {
                Func<string> baseStringToSign = () => String.Format("{0}\n{1}\n{2}\n{3}\n{4}",
                                                                    queryParams.Value<string>(ParamPermissions),
                                                                    queryParams.Value<string>(ParamStartTime),
                                                                    queryParams.Value<string>(ParamExpiryTime),
                                                                    GetCanonicalizedResource(requestUriParts, resourceType, sasVersion),
                                                                    queryParams.Value<string>(ParamStoredPolicy));
                Func<string> ipAndProtocolSnippet = () => String.Format("{0}\n{1}\n",
                                                                    queryParams.Value<string>(ParamSourceIP),
                                                                    queryParams.Value<string>(ParamProtocol));
                Func<string> v2012_02_12StringToSign = () => String.Format("{0}\n{1}{2}",
                                                                            baseStringToSign(),
                                                                            sasVersion >= StorageServiceVersions.Version_2015_04_05 ? ipAndProtocolSnippet() : String.Empty,
                                                                            sasVersion.ToVersionString());

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
            }
            else
            {
                stringToSignFactory = () => String.Format("{0}\n{1}\n{2}\n{3}\n{4}\n{5}\n{6}\n{7}\n{8}\n",
                                                                SharedKeySignature.AccountName,
                                                                queryParams.Value<string>(ParamPermissions),
                                                                queryParams.Value<string>(ParamServices),
                                                                queryParams.Value<string>(ParamResourceTypeEx),
                                                                queryParams.Value<string>(ParamStartTime),
                                                                queryParams.Value<string>(ParamExpiryTime),
                                                                queryParams.Value<string>(ParamSourceIP),
                                                                queryParams.Value<string>(ParamProtocol),
                                                                sasVersion.ToVersionString());
            }
            string signature = queryParams.Value<string>(ParamSignature);
            var usingPrimaryKey = new[] { true, false };
            string stringToSign = stringToSignFactory();
            int matchIndex = Array.FindIndex(usingPrimaryKey, usePrimaryKey => VerifySignature(signature, usePrimaryKey, stringToSign));
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

        static string GetCanonicalizedResource(RequestUriParts uriParts, string resourceType, DateTimeOffset sasVersion)
        {
            // The implementation seems out of sync with the documentation wrt leading slash - going with the implementation & 
            // hoping we don't have to support both formats!
            if (resourceType[0] == 'c')
            {
                return String.Format("{0}/{1}/{2}", 
                    sasVersion >= StorageServiceVersions.Version_2015_02_21 ? "/blob" : String.Empty,
                    SharedKeySignature.AccountName, 
                    uriParts.Container);
            }
            else if (resourceType[0] == 'b')
            {
                return String.Format("{0}/{1}{2}",
                    sasVersion >= StorageServiceVersions.Version_2015_02_21 ? "/blob" : String.Empty,
                    SharedKeySignature.AccountName, 
                    uriParts.PublicUriPath);
            }
            return String.Empty;
        }

        static bool VerifySignature(string signature, bool usePrimaryKey, string stringToSign)
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

        static SharedAccessBlobPermissions PermissionsFromString(string input)
        {
            // Allow other characters to be present for future versions of the service
            SharedAccessBlobPermissions retval = SharedAccessBlobPermissions.None;
            foreach (char ch in input)
            {
                switch (ch)
                {
                    case 'r':
                        retval |= SharedAccessBlobPermissions.Read;
                        break;

                    case 'w':
                    case 'c':
                        // Make Create & Write synonyms for now
                        retval |= SharedAccessBlobPermissions.Write;
                        break;

                    case 'd':
                        retval |= SharedAccessBlobPermissions.Delete;
                        break;

                    case 'l':
                        retval |= SharedAccessBlobPermissions.List;
                        break;
                }
            }
            return retval;
        }

        static bool IsProtocolMatched(IHttpRequestWrapper request, string allowedProtocols)
        {
            if (String.IsNullOrWhiteSpace(allowedProtocols))
            {
                return true;
            }
            string requestScheme = request.Url.Scheme.ToLowerInvariant();
            return allowedProtocols.Split(',')
                .Any(protocol => String.Equals(requestScheme, protocol, StringComparison.OrdinalIgnoreCase));
        }

        static bool IsSourceAddressInRange(IHttpRequestWrapper request, string ipAddressOrRange)
        {
            // We don't have a reliable way of determining the client address (headers can be spoofed),
            // therefore we only do structural checks here without actually verifying the address.
            if (String.IsNullOrWhiteSpace(ipAddressOrRange))
            {
                return true;
            }
            IPAddress parseCheck;
            var parts = ipAddressOrRange.Split('-');
            if (parts.Length <= 2)
            {
                return parts.All(address => IPAddress.TryParse(address, out parseCheck));
            }
            return false;
        }
    }
}