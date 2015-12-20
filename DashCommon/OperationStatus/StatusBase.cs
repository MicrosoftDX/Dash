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
            try
            {
                GetStatusTable().CreateIfNotExists();
            }
            catch (Exception ex)
            {
                DashTrace.TraceWarning("Error creating/referencing account status table: {0}. Details: {1}", _tableName, ex);
            }
        }

        public abstract Task UpdateStatus(StatusItemBase statusItem);

        protected CloudTable GetStatusTable()
        {
            if (this._storageAccount != null)
            {
                return this._storageAccount.CreateCloudTableClient().GetTableReference(_tableName);
            }
            return null;
        }
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
            return await GetStatus(() => ExecuteAsync(TableOperation.Retrieve(statusItemKey, String.Empty)), statusItemKey);
        }

        public async Task<T> QueryStatus<U>(TableQuery query, Func<ITableEntity, U> orderingKeySelector = null)
        {
            return await GetStatus(() => ExecuteAsync(query, orderingKeySelector), String.Empty);
        }

        public async Task<T> GetStatus(Func<Task<ITableEntity>> predicate, string itemKey)
        {
            var result = await predicate();
            if (result == null)
            {
                return this._newItemFactory(itemKey, this);
            }
            else
            {
                return this._readItemFactory((DynamicTableEntity)result, this);
            }
        }

        public override async Task UpdateStatus(StatusItemBase statusItem)
        {
            await ExecuteAsync(TableOperation.InsertOrReplace(_updateItemFactory((T)statusItem)));
        }

        private async Task<ITableEntity> ExecuteAsync(TableOperation operation)
        {
            try
            {
                var table = GetStatusTable();
                if (table != null)
                {
                    var result = await table.ExecuteAsync(operation);
                    if (result != null)
                    {
                        return (ITableEntity)result.Result;
                    }
                }
            }
            catch (Exception ex)
            {
                DashTrace.TraceWarning("Error interacting with status table. Details: {0}", ex);
            }
            return null;
        }

        private async Task<ITableEntity> ExecuteAsync<U>(TableQuery query, Func<ITableEntity, U> orderingKeySelector = null)
        {
            try
            {
                var table = GetStatusTable();
                if (table != null)
                {
                    IEnumerable<ITableEntity> statuses = await table.ExecuteQuerySegmentedAsync(query, null);
                    if (orderingKeySelector != null)
                    { 
                        statuses = statuses.OrderByDescending(orderingKeySelector);
                    }
                    return statuses.FirstOrDefault();
                }
            }
            catch (Exception ex)
            {
                DashTrace.TraceWarning("Error querying status table. Details: {0}", ex);
            }
            return null;
        }

        protected const string CollectionDelimiter      = "|";
        protected static char CollectionDelimiterChar   = CollectionDelimiter[0];

        enum TypeClasses
        {
            Unknown,
            Enum,
            List,
            Enumerable,
            String,
        }

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
                    switch (GetTypeClass<U>())
                    { 
                        case TypeClasses.Enum:
                            return (U)Enum.Parse(typeof(U), attributeValue, true);

                        case TypeClasses.List:
                            return (U)(object)attributeValue.Split(CollectionDelimiterChar).ToList();

                        case TypeClasses.Enumerable:
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
            switch (GetTypeClass<U>())
            { 
                case TypeClasses.Enumerable:
                case TypeClasses.List:
                    stringValue = String.Join(CollectionDelimiter, (IEnumerable<string>)value);
                    break;

                case TypeClasses.String:
                    stringValue = value as string;
                    break;

                default:
                    stringValue = value.ToString();
                    break;
            }
            return EntityProperty.GeneratePropertyForString(stringValue);
        }

        private static TypeClasses GetTypeClass<U>()
        {
            if (typeof(U).IsEnum)
            {
                return TypeClasses.Enum;
            }
            else if (typeof(IList<string>).IsAssignableFrom(typeof(U)))
            {
                return TypeClasses.List;
            }
            else if (typeof(IEnumerable<string>).IsAssignableFrom(typeof(U)))
            {
                return TypeClasses.Enumerable;
            }
            else if (typeof(U) == typeof(string))
            {
                return TypeClasses.String;
            }
            return TypeClasses.Unknown;
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
