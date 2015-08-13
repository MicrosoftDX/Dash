//     Copyright (c) Microsoft Corporation.  All rights reserved.

using System;
using System.Diagnostics;
using System.Net;
using System.Threading.Tasks;
using Microsoft.Dash.Common.Diagnostics;
using Microsoft.WindowsAzure.Storage.Table;

namespace Microsoft.Dash.Common.OperationStatus
{
    public class AccountStatus : StatusBase<AccountStatus.Account>
    {
        public enum States
        {
            Unknown,
            Healthy,
            Warning,
            Error
        }

        public class Account : StatusItemBase
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
                await base.UpdateStatus(message, traceLevel);
            }
        }

        const string StatusTableName    = "AccountStatus";
        const string StatusFieldMessage = "Message";
        const string StatusFieldState   = "State";

        public AccountStatus() 
            : base(StatusTableName, 
            (accountName, statusHandler) => new Account
            {
                StatusHandler = (AccountStatus)statusHandler,
                Name = accountName,
                State = States.Unknown,
            },
            (entity, statusHandler) => new Account
            {
                StatusHandler = (AccountStatus)statusHandler,
                Name = entity.PartitionKey,
                State = EntityAttribute(entity, StatusFieldState, States.Unknown),
                Message = EntityAttribute(entity, StatusFieldMessage, String.Empty),
            },
            (status) => {
                var entity = new DynamicTableEntity(status.Name, String.Empty);
                entity[StatusFieldMessage] = EntityProperty.GeneratePropertyForString(status.Message);
                entity[StatusFieldState] = EntityProperty.GeneratePropertyForString(status.State.ToString());
                return entity;
            })
        {

        }

        public static async Task<Account> GetAccountStatus(string accountName)
        {
            return await (new AccountStatus()).GetStatus(accountName);
        }
    }
}
