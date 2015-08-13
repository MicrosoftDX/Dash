//     Copyright (c) Microsoft Corporation.  All rights reserved.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Microsoft.Dash.Common.Diagnostics;
using Microsoft.Dash.Common.Utils;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Table;

namespace Microsoft.Dash.Common.OperationStatus
{
    public abstract class StatusBase
    {
        protected CloudStorageAccount _storageAccount;
        protected string _tableName;

        public void UpdateCloudStorageAccount(CloudStorageAccount newAccount)
        {
            _storageAccount = newAccount;
        }

        public abstract Task UpdateStatus(StatusItemBase statusItem);
    }

    public class StatusBase<T> : StatusBase where T : StatusItemBase
    {
        private Func<string, StatusBase<T>, T> _newItemFactory;
        private Func<DynamicTableEntity, StatusBase<T>, T> _readItemFactory;
        private Func<T, ITableEntity> _updateItemFactory;

        protected StatusBase(string tableName, 
            Func<string, StatusBase<T>, T> newItemFactory, 
            Func<DynamicTableEntity, StatusBase<T>, T> readItemFactory, 
            Func<T, ITableEntity> updateItemFactory,
            CloudStorageAccount account = null)
        {
            this._tableName = tableName;
            this._storageAccount = account ?? DashConfiguration.NamespaceAccount;
            this._newItemFactory = newItemFactory;
            this._readItemFactory = readItemFactory;
            this._updateItemFactory = updateItemFactory;
            try
            {
                GetStatusTable().CreateIfNotExists();
            }
            catch (Exception ex)
            {
                DashTrace.TraceWarning("Error creating/referencing account status table: {0}. Details: {1}", tableName, ex);
            }
        }

        public async Task<T> GetStatus(string statusItemKey)
        {
            var result = await ExecuteAsync(TableOperation.Retrieve(statusItemKey, String.Empty));
            if (result == null || result.HttpStatusCode == (int)HttpStatusCode.NotFound)
            {
                return this._newItemFactory(statusItemKey, this);
            }
            else
            {
                return this._readItemFactory((DynamicTableEntity)result.Result, this);
            }
        }

        public override async Task UpdateStatus(StatusItemBase statusItem)
        {
            await ExecuteAsync(TableOperation.InsertOrReplace(_updateItemFactory((T)statusItem)));
        }

        private async Task<TableResult> ExecuteAsync(TableOperation operation)
        {
            try
            {
                var table = GetStatusTable();
                if (table != null)
                {
                    return await GetStatusTable().ExecuteAsync(operation);
                }
            }
            catch (Exception ex)
            {
                DashTrace.TraceWarning("Error interacting with status table. Details: {0}", ex);
            }
            return null;
        }

        protected CloudTable GetStatusTable()
        {
            if (this._storageAccount != null)
            {
                return this._storageAccount.CreateCloudTableClient().GetTableReference(_tableName);
            }
            return null;
        }

        protected const string CollectionDelimiter      = "|";
        protected static char CollectionDelimiterChar   = CollectionDelimiter[0];

        protected static U EntityAttribute<U>(DynamicTableEntity entity, string attributeName, U defaultValue)
        {
            string attributeValue = null;
            if (entity.Properties.ContainsKey(attributeName))
            {
                attributeValue = entity[attributeName].StringValue;
            }
            if (!String.IsNullOrWhiteSpace(attributeValue))
            {
                try
                {
                    if (typeof(U).IsEnum)
                    {
                        return (U)Enum.Parse(typeof(U), attributeValue, true);
                    }
                    else if (typeof(U) is IList)
                    {
                        return (U)(object)attributeValue.Split(CollectionDelimiterChar).ToList();
                    }
                    else if (typeof(U) is IEnumerable)
                    {
                        return (U)(object)attributeValue.Split(CollectionDelimiterChar);
                    }
                    return (U)Convert.ChangeType(attributeValue, typeof(U));
                }
                catch
                {
                }
            }
            return defaultValue;
        }

        protected static EntityProperty EntityPropertyValue<U>(U value)
        {
            string stringValue;
            if (typeof(U) is IEnumerable)
            {
                stringValue = String.Join(CollectionDelimiter, (IEnumerable<string>)value);
            }
            else
            {
                stringValue = value.ToString();
            }
            return EntityProperty.GeneratePropertyForString(stringValue);
        }
    }

    public class StatusItemBase
    {
        public StatusBase StatusHandler { get; set; }

        protected async Task UpdateStatus(string message, TraceLevel traceLevel)
        {
            await this.StatusHandler.UpdateStatus(this);
            switch (traceLevel)
            {
                case TraceLevel.Error:
                    DashTrace.TraceError(message);
                    break;

                case TraceLevel.Warning:
                    DashTrace.TraceWarning(message);
                    break;

                case TraceLevel.Info:
                case TraceLevel.Verbose:
                    DashTrace.TraceInformation(message);
                    break;
            }
        }
    }
}
