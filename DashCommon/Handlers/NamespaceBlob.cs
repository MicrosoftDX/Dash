//     Copyright (c) Microsoft Corporation.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;

namespace Microsoft.Dash.Common.Handlers
{
    public class NamespaceBlob
    {
        const string MetadataNameAccount    = "accountname";
        const string MetadataNameContainer  = "container";
        const string MetadataNameBlobName   = "blobname";
        const string MetadataNameDeleteFlag = "todelete";

        const string AccountDelimiter       = "|";

        static Random _dataAccountSelector  = new Random();

        CloudBlockBlob _namespaceBlob;
        bool _blobExists;
        IList<string> _dataAccounts;

        public NamespaceBlob(CloudBlockBlob namespaceBlob)
        {
            _namespaceBlob = namespaceBlob;
        }

        public async static Task<NamespaceBlob> FetchForBlobAsync(CloudBlockBlob namespaceBlob)
        {
            var retval = new NamespaceBlob(namespaceBlob);
            await retval.RefreshAsync();
            return retval;
        }

        public async Task RefreshAsync()
        { 
            // Exists() populates attributes
            _blobExists = await _namespaceBlob.ExistsAsync();
        }

        public async Task SaveAsync()
        {
            if (_dataAccounts != null)
            {
                _namespaceBlob.Metadata[MetadataNameAccount] = String.Join(AccountDelimiter, _dataAccounts);
            }
            if (!_blobExists)
            {
                await _namespaceBlob.UploadTextAsync("", Encoding.UTF8, AccessCondition.GenerateIfNoneMatchCondition("*"), null, null);
            }
            else
            {
                await _namespaceBlob.SetMetadataAsync(AccessCondition.GenerateIfMatchCondition(_namespaceBlob.Properties.ETag), null, null);
            }
        }

        public async Task MarkForDeletionAsync()
        {
            await RefreshAsync();
            this.IsMarkedForDeletion = true;
            await SaveAsync();
        }

        public async Task<bool> ExistsAsync(bool forceRefresh = false)
        {
            if (forceRefresh)
            {
                _blobExists = await _namespaceBlob.ExistsAsync();
            }
            return _blobExists;
        }

        private string TryGetMetadataValue(string metadataName)
        {
            string retval = String.Empty;
            _namespaceBlob.Metadata.TryGetValue(metadataName, out retval);
            return retval;
        }

        public string PrimaryAccountName
        {
            get { return this.DataAccounts.FirstOrDefault(); }
            set
            {
                var accounts = this.DataAccounts;
                accounts.Clear();
                accounts.Add(value);
            }
        }

        public IList<string> DataAccounts
        {
            get 
            {
                if (_dataAccounts == null)
                {
                    lock (this)
                    {
                        if (_dataAccounts == null)
                        {
                            _dataAccounts = TryGetMetadataValue(MetadataNameAccount)
                                .Split(AccountDelimiter[0]);
                        }
                    }
                }
                return _dataAccounts; 
            }
        }

        public string SelectDataAccount
        {
            get
            {
                var dataAccounts = this.DataAccounts;
                if (!dataAccounts.Any())
                {
                    return String.Empty;
                }
                return dataAccounts[_dataAccountSelector.Next(dataAccounts.Count())];
            }
        }

        public bool IsReplicated
        {
            get { return this.DataAccounts.Count > 1; }
        }

        public string Container
        {
            get { return TryGetMetadataValue(MetadataNameContainer); }
            set { _namespaceBlob.Metadata[MetadataNameContainer] = value; }
        }

        public string BlobName
        {
            get { return TryGetMetadataValue(MetadataNameBlobName); }
            set { _namespaceBlob.Metadata[MetadataNameBlobName] = value; }
        }

        public bool IsMarkedForDeletion
        {
            get
            {
                bool retval = false;
                string deleteFlag = TryGetMetadataValue(MetadataNameDeleteFlag);
                bool.TryParse(deleteFlag, out retval);
                return retval;
            }
            set
            {
                if (value)
                {
                    _namespaceBlob.Metadata[MetadataNameDeleteFlag] = "true";
                }
                else
                {
                    _namespaceBlob.Metadata.Remove(MetadataNameDeleteFlag);
                }
            }
        }
    }
}