//     Copyright (c) Microsoft Corporation.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Table;

namespace Microsoft.Dash.Common.OperationStatus
{
    public class UpdateConfigStatus : StatusBase<UpdateConfigStatus.ConfigUpdate>
    {
        public enum States
        {
            Unknown,
            NotStarted,
            CreatingAccounts,
            ImportingAccounts,
            UpdatingService,
            Completed,
            Failed
        }

        public class ConfigUpdate : StatusItemBase
        {
            public string OperationId { get; set; }
            public States State { get; set; }
            public DateTime StartTime { get; set; }
            public DateTime EndTime { get; set; }
            public IList<string> AccountsToBeCreated { get; set; }
            public IList<string> AccountsToBeImported { get; set; }
            public IList<string> AccountsImportedSuccess { get; set; }
            public IList<string> AccountsImportedFailed { get; set; }
            public string CloudServiceUpdateOperationId { get; set; }
            public string StatusMessage { get; set; }

            public async Task UpdateStatus(States newState, string messageFormat, params string[] args)
            {
                await UpdateStatus(String.Format(messageFormat, args), newState, TraceLevel.Info);
            }

            public async Task UpdateStatus(string message, States newState, TraceLevel traceLevel)
            {
                this.StatusMessage = message;
                this.State = newState;
                await base.UpdateStatus(message, traceLevel);
            }
        }

        const string FieldState                         = "State";
        const string FieldStartTime                     = "StartTime";
        const string FieldEndTime                       = "EndTime";
        const string FieldMessage                       = "StatusMessage";
        const string FieldAccountsToBeCreated           = "AccountsToBeCreated";
        const string FieldAccountsToBeImported          = "AccountsToBeImported";
        const string FieldAccountsImportedSuccess       = "AccountsImportedSuccess";
        const string FieldAccountsImportedFailed        = "AccountsImportedFailed";
        const string FieldCloudServiceUpdateOperationId = "CloudServiceUpdateOperationId";

        public UpdateConfigStatus(CloudStorageAccount namespaceAccount = null) 
            : base("UpdateStatus", 
            (operationId, statusHandler) => new ConfigUpdate
            {
                StatusHandler = statusHandler,
                OperationId = operationId,
                State = States.Unknown,
                StartTime = DateTime.UtcNow,
                AccountsToBeCreated = new List<string>(),
                AccountsToBeImported = new List<string>(),
                AccountsImportedFailed = new List<string>(),
                AccountsImportedSuccess= new List<string>(),
            },
            (entity, statusHandler) => new ConfigUpdate
            {
                StatusHandler = statusHandler,
                OperationId = entity.PartitionKey,
                State = EntityAttribute(entity, FieldState, States.NotStarted),
                StartTime = EntityAttribute(entity, FieldStartTime, DateTime.UtcNow),
                EndTime = EntityAttribute(entity, FieldEndTime, DateTime.MinValue),
                StatusMessage = EntityAttribute(entity, FieldMessage, String.Empty),
                AccountsToBeCreated = EntityAttribute(entity, FieldAccountsToBeCreated, new List<string>()),
                AccountsToBeImported = EntityAttribute(entity, FieldAccountsToBeImported, new List<string>()),
                AccountsImportedFailed = EntityAttribute(entity, FieldAccountsImportedFailed, new List<string>()),
                AccountsImportedSuccess = EntityAttribute(entity, FieldAccountsImportedSuccess, new List<string>()),
                CloudServiceUpdateOperationId = EntityAttribute(entity, FieldCloudServiceUpdateOperationId, String.Empty),
            },
            (status) => {
                var entity = new DynamicTableEntity(status.OperationId, String.Empty);
                entity[FieldState] = EntityPropertyValue(status.State);
                entity[FieldStartTime] = EntityPropertyValue(status.StartTime);
                entity[FieldEndTime] = EntityPropertyValue(status.EndTime);
                entity[FieldMessage] = EntityPropertyValue(status.StatusMessage);
                entity[FieldAccountsToBeCreated] = EntityPropertyValue(status.AccountsToBeCreated);
                entity[FieldAccountsToBeImported] = EntityPropertyValue(status.AccountsToBeImported);
                entity[FieldAccountsImportedSuccess] = EntityPropertyValue(status.AccountsImportedSuccess);
                entity[FieldAccountsImportedFailed] = EntityPropertyValue(status.AccountsImportedFailed);
                entity[FieldCloudServiceUpdateOperationId] = EntityPropertyValue(status.CloudServiceUpdateOperationId);
                return entity;
            },
            namespaceAccount)
        {

        }

        public static async Task<ConfigUpdate> GetConfigUpdateStatus(string operationId, CloudStorageAccount namespaceAccount = null)
        {
            return await (new UpdateConfigStatus(namespaceAccount)).GetStatus(operationId);
        }
    }
}
