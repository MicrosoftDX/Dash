//     Copyright (c) Microsoft Corporation.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;

namespace Microsoft.Dash.Common.Handlers
{
    /// <summary>
    /// NamespaceBlob that lives in the Namespace Storage Account
    /// </summary>
    public class NamespaceBlobCloud : INamespaceBlob
    {
        private const string MetadataNameAccount    = "accountname";
        private const string MetadataNameContainer  = "container";
        private const string MetadataNameBlobName   = "blobname";
        private const string MetadataNameDeleteFlag = "todelete";

        private bool _isLoaded = false;
        private bool _cloudBlockBlobExists = false;

        const string AccountDelimiter               = "|";
        static readonly char AccountDelimiterChar   = AccountDelimiter[0];


        IList<string> _dataAccounts;

        private readonly Func<CloudBlockBlob> _getCloudBlockBlob;

        private CloudBlockBlob _cloudBlockBlob;
        private CloudBlockBlob CloudBlockBlob
        {
            get
            {
                if (_isLoaded == false)
                {
                    _cloudBlockBlob = _getCloudBlockBlob();
                    _cloudBlockBlobExists = _cloudBlockBlob.Exists();
                    _isLoaded = true;
                }
                return _cloudBlockBlob;
            }
        }

        public virtual string AccountName
        {
            get { return TryGetMetadataValue(MetadataNameAccount); }
            set { CloudBlockBlob.Metadata[MetadataNameAccount] = value; }
        }

        public virtual string Container
        {
            get { return TryGetMetadataValue(MetadataNameContainer); }
            set { CloudBlockBlob.Metadata[MetadataNameContainer] = value; }
        }

        public virtual string BlobName
        {
            get { return TryGetMetadataValue(MetadataNameBlobName); }
            set { CloudBlockBlob.Metadata[MetadataNameBlobName] = value; }
        }

        public virtual bool? IsMarkedForDeletion
        {
            get
            {
                var retval = false;
                var deleteFlag = TryGetMetadataValue(MetadataNameDeleteFlag);
                bool.TryParse(deleteFlag, out retval);
                return retval;
            }
            set
            {
                if (value == null || value == false)
                {
                    CloudBlockBlob.Metadata.Remove(MetadataNameDeleteFlag);
                }
                else
                {
                    CloudBlockBlob.Metadata[MetadataNameDeleteFlag] = Boolean.TrueString;
                }
            }
        }

        public async Task SaveAsync()
        {
            if (_dataAccounts != null && _dataAccounts.Count > 0)
            {
                CloudBlockBlob.Metadata[MetadataNameAccount] = String.Join(AccountDelimiter, _dataAccounts);
            }

            if (await ExistsAsync())
            {
                await CloudBlockBlob.SetMetadataAsync(AccessCondition.GenerateIfMatchCondition(CloudBlockBlob.Properties.ETag), null, null);
            }
            else
            {
                await CloudBlockBlob.UploadTextAsync("", Encoding.UTF8, AccessCondition.GenerateIfNoneMatchCondition("*"), null, null);
                _cloudBlockBlobExists = true;
            }
        }

        public async Task<bool> ExistsAsync(bool forceRefresh = false)
        {
            if (forceRefresh || !_isLoaded)
            {
                _cloudBlockBlobExists = await CloudBlockBlob.ExistsAsync();
            }
            return _cloudBlockBlobExists;
        }

        /// <summary>
        /// NamespaceBlobCloud
        /// </summary>
        /// <param name="getCloudBlockBlob">Function delegate to retrieve the NamespaceBlob from Storage</param>
        public NamespaceBlobCloud(Func<CloudBlockBlob> getCloudBlockBlob)
        {
            _getCloudBlockBlob = getCloudBlockBlob;
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

        public IList<string> DataAccounts        {            get             {                if (_dataAccounts == null)                {                    lock (this)                    {                        if (_dataAccounts == null)                        {                            string accounts = TryGetMetadataValue(MetadataNameAccount);                            if (String.IsNullOrWhiteSpace(accounts))                            {                                _dataAccounts = new List<string>();                            }                            else                            {                                _dataAccounts = accounts                                    .Split(AccountDelimiterChar)                                    .Select(account => account.Trim())                                    .ToList();                            }                        }                    }                }                return _dataAccounts;             }        }
        public bool AddDataAccount(string dataAccount)        {            var dataAccounts = this.DataAccounts;            if (!dataAccounts.Contains(dataAccount, StringComparer.OrdinalIgnoreCase))            {                dataAccounts.Add(dataAccount);                return true;            }
            return false;        }        public bool RemoveDataAccount(string dataAccount)        {            var dataAccounts = this.DataAccounts;            if (dataAccounts.Contains(dataAccount, StringComparer.OrdinalIgnoreCase))            {                dataAccounts.RemoveAt(((List<string>)dataAccounts)                                            .FindIndex(account => String.Equals(account, dataAccount, StringComparison.OrdinalIgnoreCase)));
                return true;            }
            return false;        }
        public bool IsReplicated        {            get { return this.DataAccounts.Count > 1; }        }
        private string TryGetMetadataValue(string metadataName)
        {
            string retval;
            CloudBlockBlob.Metadata.TryGetValue(metadataName, out retval);
            return retval;
        }
    }
}
