//     Copyright (c) Microsoft Corporation.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using Microsoft.Dash.Server.Utils;
using System.Net.Http;

namespace Microsoft.Dash.Server.Handlers
{
    public enum StorageOperationTypes
    {
        Unknown,
        ListContainers,
        SetBlobServiceProperties,
        GetBlobServiceProperties,
        PreflightBlobRequest,
        GetBlobServiceStats,
        CreateContainer,
        GetContainerProperties,
        GetContainerMetadata,
        SetContainerMetadata,
        GetContainerACL,
        SetContainerACL,
        LeaseContainer,
        DeleteContainer,
        ListBlobs,
        PutBlob,
        GetBlob,
        GetBlobProperties,
        SetBlobProperties,
        GetBlobMetadata,
        SetBlobMetadata,
        DeleteBlob,
        LeaseBlob,
        SnapshotBlob,
        CopyBlob,
        AbortCopyBlob,
        PutBlock,
        PutBlockList,
        GetBlockList
    }

    public static class StorageOperations
    {
        class RequestAttributes
        {
            public HttpMethod Method { get; set; }
            public RequestUriParts UriParts { get; set; }
            public RequestQueryParameters QueryParams { get; set; }
            public RequestHeaders Headers { get; set; }
        }

        static StorageOperationTypes GetPutOrCopyBlobOperation(RequestAttributes request)
        {
            if (request.Headers.Contains("x-ms-copy-source"))
            {
                return StorageOperationTypes.CopyBlob;
            }
            return StorageOperationTypes.PutBlob;
        }

        enum OperationGroup
        {
            Account,
            Container,
            Blob,
        }

        class OperationDefinition
        {
            public OperationDefinition(OperationGroup group, HttpMethod method1, HttpMethod method2, string comp, Func<RequestAttributes, StorageOperationTypes> operation)
            {
                this.Group = group;
                this.Method1 = method1;
                this.Method2 = method2;
                this.Comp = comp;
                this.Operation = operation;
            }

            public OperationDefinition(OperationGroup group, HttpMethod method1, HttpMethod method2, string comp, StorageOperationTypes operation)
                : this(group, method1, method2, comp, (attribs) => operation)
            {
            }

            public OperationGroup Group { get; set; }
            public HttpMethod Method1 { get; set; }
            public HttpMethod Method2 { get; set; }
            public string Comp { get; set; }
            public Func<RequestAttributes, StorageOperationTypes> Operation { get; private set; }
        }

        static readonly List<OperationDefinition> _operations = new List<OperationDefinition>()
        {
            new OperationDefinition(OperationGroup.Account,     HttpMethod.Get,     null,               "list",         StorageOperationTypes.ListContainers),
            new OperationDefinition(OperationGroup.Account,     HttpMethod.Get,     null,               "properties",   StorageOperationTypes.GetBlobServiceProperties),
            new OperationDefinition(OperationGroup.Account,     HttpMethod.Get,     null,               "stats",        StorageOperationTypes.GetBlobServiceStats),
            new OperationDefinition(OperationGroup.Account,     HttpMethod.Put,     null,               "properties",   StorageOperationTypes.SetBlobServiceProperties),
            new OperationDefinition(OperationGroup.Container,   HttpMethod.Get,     HttpMethod.Head,    "",             StorageOperationTypes.GetContainerProperties),
            new OperationDefinition(OperationGroup.Container,   HttpMethod.Get,     HttpMethod.Head,    "metadata",     StorageOperationTypes.GetContainerMetadata),
            new OperationDefinition(OperationGroup.Container,   HttpMethod.Get,     HttpMethod.Head,    "acl",          StorageOperationTypes.GetContainerACL),
            new OperationDefinition(OperationGroup.Container,   HttpMethod.Get,     null,               "list",         StorageOperationTypes.ListBlobs),
            new OperationDefinition(OperationGroup.Container,   HttpMethod.Put,     null,               "",             StorageOperationTypes.CreateContainer),
            new OperationDefinition(OperationGroup.Container,   HttpMethod.Put,     null,               "metadata",     StorageOperationTypes.SetContainerMetadata),
            new OperationDefinition(OperationGroup.Container,   HttpMethod.Put,     null,               "acl",          StorageOperationTypes.SetContainerACL),
            new OperationDefinition(OperationGroup.Container,   HttpMethod.Put,     null,               "lease",        StorageOperationTypes.LeaseContainer),
            new OperationDefinition(OperationGroup.Container,   HttpMethod.Delete,  null,               "",             StorageOperationTypes.DeleteContainer),
            new OperationDefinition(OperationGroup.Blob,        HttpMethod.Get,     null,               "",             StorageOperationTypes.GetBlob),
            new OperationDefinition(OperationGroup.Blob,        HttpMethod.Get,     HttpMethod.Head,    "metadata",     StorageOperationTypes.GetBlobMetadata),
            new OperationDefinition(OperationGroup.Blob,        HttpMethod.Get,     null,               "blocklist",    StorageOperationTypes.GetBlockList),
            new OperationDefinition(OperationGroup.Blob,        HttpMethod.Head,    null,               "",             StorageOperationTypes.GetBlobProperties),
            new OperationDefinition(OperationGroup.Blob,        HttpMethod.Put,     null,               "",             GetPutOrCopyBlobOperation),
            new OperationDefinition(OperationGroup.Blob,        HttpMethod.Put,     null,               "properties",   StorageOperationTypes.SetBlobProperties),
            new OperationDefinition(OperationGroup.Blob,        HttpMethod.Put,     null,               "metadata",     StorageOperationTypes.SetBlobMetadata),
            new OperationDefinition(OperationGroup.Blob,        HttpMethod.Put,     null,               "lease",        StorageOperationTypes.LeaseBlob),
            new OperationDefinition(OperationGroup.Blob,        HttpMethod.Put,     null,               "snapshot",     StorageOperationTypes.SnapshotBlob),
            new OperationDefinition(OperationGroup.Blob,        HttpMethod.Put,     null,               "copy",         StorageOperationTypes.AbortCopyBlob),
            new OperationDefinition(OperationGroup.Blob,        HttpMethod.Put,     null,               "block",        StorageOperationTypes.PutBlock),
            new OperationDefinition(OperationGroup.Blob,        HttpMethod.Put,     null,               "blocklist",    StorageOperationTypes.PutBlockList),
            new OperationDefinition(OperationGroup.Blob,        HttpMethod.Delete,  null,               "",             StorageOperationTypes.DeleteBlob),
        };

        static IDictionary<HttpMethod, Dictionary<string, Func<RequestAttributes, StorageOperationTypes>>> CreateGroupDefinitions(OperationGroup group)
        {
            return _operations
                .Where(op => op.Group == group)
                .SelectMany(op => new[] { Tuple.Create(op.Method1, op) }.Concat( op.Method2 != null ? new[] { Tuple.Create(op.Method2, op)} : Enumerable.Empty<Tuple<HttpMethod, OperationDefinition>>()))
                .GroupBy(op => op.Item1, op => op.Item2)
                .ToDictionary(methodOps => methodOps.Key, methodOps => methodOps
                    .ToDictionary(op => op.Comp, op => op.Operation, StringComparer.OrdinalIgnoreCase));
        }

        static readonly IDictionary<HttpMethod, Dictionary<string, Func<RequestAttributes, StorageOperationTypes>>>
            _accountOperations = CreateGroupDefinitions(OperationGroup.Account),
            _containerOperations = CreateGroupDefinitions(OperationGroup.Container),
            _blobOperations = CreateGroupDefinitions(OperationGroup.Blob);

        public static StorageOperationTypes GetBlobOperation(IHttpRequestWrapper request)
        {
            return GetBlobOperation(request.HttpMethod, RequestUriParts.Create(request.Url), RequestQueryParameters.Create(request.QueryParameters), RequestHeaders.Create(request.Headers));
        }

        public static StorageOperationTypes GetBlobOperation(string requestMethod, RequestUriParts requestUriParts, RequestQueryParameters queryParams, RequestHeaders headers)
        {
            var requestAttributes = new RequestAttributes
            {
                Method = new HttpMethod(requestMethod),
                UriParts = requestUriParts,
                QueryParams = queryParams,
                Headers = headers,
            };
            if (requestUriParts.IsAccountRequest)
            {
                return LookupBlobOperation(requestAttributes, _accountOperations);
            }
            else if (requestUriParts.IsContainerRequest)
            {
                return LookupBlobOperation(requestAttributes, _containerOperations);
            }
            else if (requestUriParts.IsBlobRequest)
            {
                return LookupBlobOperation(requestAttributes, _blobOperations);
            }
            else
            {
                System.Diagnostics.Debug.Assert(false);
            }
            return StorageOperationTypes.Unknown;
        }

        static StorageOperationTypes LookupBlobOperation(RequestAttributes attributes, IDictionary<HttpMethod, Dictionary<string, Func<RequestAttributes, StorageOperationTypes>>> lookup)
        {
            Dictionary<string, Func<RequestAttributes, StorageOperationTypes>> methodOperations;
            if (lookup.TryGetValue(attributes.Method, out methodOperations))
            {
                var compParam = attributes.QueryParams.Value("comp", String.Empty);
                Func<RequestAttributes, StorageOperationTypes> operationDispatch;
                if (methodOperations.TryGetValue(compParam, out operationDispatch))
                {
                    return operationDispatch(attributes);
                }
            }
            return StorageOperationTypes.Unknown;
        }

    }

}