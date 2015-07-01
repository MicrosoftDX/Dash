//     Copyright (c) Microsoft Corporation.  All rights reserved.

using System;
using System.Diagnostics;
using System.Net;
using System.Threading.Tasks;
using Microsoft.Dash.Common.Diagnostics;
using Microsoft.WindowsAzure.Storage.Table;

namespace Microsoft.Dash.Common.Utils
{
    public static class AccountStatus
    {
        public enum States
        {
            Unknown,
            Healthy,
            Warning,
            Error
        }

        public class Account
        {
            public string Name { get; set; }
            public string Message { get; set; }
            public States State { get; set; }

            public async Task UpdateStatusInformation(string messageFormat, params string[] args)
            {
                await UpdateStatusInformation(States.Unknown, messageFormat, args);
            }

            public async Task UpdateStatusInformation(States newState, string messageFormat, params string[] args)
            {
                await UpdateStatus(String.Format(messageFormat, args), newState, TraceLevel.Info);
            }

            public async Task UpdateStatusWarning(string messageFormat, params string[] args)
            {
                States newState = this.State;
                if (newState < States.Warning)
                {
                    newState = States.Warning;
                }
                await UpdateStatus(String.Format(messageFormat, args), newState, TraceLevel.Warning);
            }

            public async Task UpdateStatus(string message, States newState, TraceLevel traceLevel)
            {
                this.Message = message;
                if (newState != States.Unknown)
                {
                    this.State = newState;
                }
                await AccountStatus.UpdateAccountStatus(this);
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

        const string StatusTableName    = "AccountStatus";
        const string StatusFieldMessage = "Message";
        const string StatusFieldState   = "State";

        // TODO: Consider storage abstraction if we want to switch between XTable & Redis for this info
        static AccountStatus()
        {
            try
            {
                GetAccountStatusTable().CreateIfNotExists();
            }
            catch (Exception ex)
            {
                DashTrace.TraceWarning("Error creating/referencing account status table: {0}. Details: {1}", StatusTableName, ex);
            }
        }

        public static async Task<Account> GetAccountStatus(string accountName)
        {
            var result = await ExecuteAsync(TableOperation.Retrieve(accountName, String.Empty));
            if (result == null || result.HttpStatusCode == (int)HttpStatusCode.NotFound)
            {
                return new Account
                {
                    Name = accountName,
                    State = States.Unknown,
                };
            }
            else
            {
                var entity = (DynamicTableEntity)result.Result;
                return new Account
                {
                    Name = entity.PartitionKey,
                    State = EntityAttribute(entity, StatusFieldState, States.Unknown),
                    Message = EntityAttribute(entity, StatusFieldMessage, String.Empty),
                };
            }
        }

        public static async Task UpdateAccountStatus(Account status)
        {
            var entity = new DynamicTableEntity(status.Name, String.Empty);
            entity[StatusFieldMessage] = EntityProperty.GeneratePropertyForString(status.Message);
            entity[StatusFieldState] = EntityProperty.GeneratePropertyForString(status.State.ToString());
            await ExecuteAsync(TableOperation.InsertOrReplace(entity));
        }

        private static async Task<TableResult> ExecuteAsync(TableOperation operation)
        {
            try
            {
                return await GetAccountStatusTable().ExecuteAsync(operation);
            }
            catch (Exception ex)
            {
                DashTrace.TraceWarning("Error interacting with account status. Details: {0}", ex);
            }
            return null;
        }

        private static T EntityAttribute<T>(DynamicTableEntity entity, string attributeName, T defaultValue)
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
                    if (typeof(T).IsEnum)
                    {
                        return (T)Enum.Parse(typeof(T), attributeValue, true);
                    }
                    return (T)Convert.ChangeType(attributeValue, typeof(T));
                }
                catch
                {
                }
            }
            return defaultValue;
        }

        private static CloudTable GetAccountStatusTable()
        {
            return DashConfiguration.NamespaceAccount.CreateCloudTableClient().GetTableReference(StatusTableName);
        }
    }
}
