//     Copyright (c) Microsoft Corporation.  All rights reserved.

using System;
using System.Threading.Tasks;
using System.Web.Http;
using DashServer.ManagementAPI.Models;
using Microsoft.Dash.Common.OperationStatus;
using Microsoft.Dash.Common.Processors;
using Microsoft.Dash.Common.ServiceManagement;
using Microsoft.WindowsAzure.Storage;

namespace DashServer.ManagementAPI.Controllers
{
    [Authorize]
    public class OperationsController : DelegatedAuthController
    {
        [HttpGet, ActionName("Index")]
        public async Task<IHttpActionResult> Get(string id, string storageConnectionStringMaster = null)
        {
            return await DoActionAsync("OperationsController.Get", async () =>
            {
                CloudStorageAccount namespaceAccount = null;
                if (!String.IsNullOrEmpty(storageConnectionStringMaster))
                {
                    CloudStorageAccount.TryParse(storageConnectionStringMaster, out namespaceAccount);
                }
                var operationStatus = await UpdateConfigStatus.GetConfigUpdateStatus(id, namespaceAccount);
                if (operationStatus.State == UpdateConfigStatus.States.Unknown)
                {
                    return NotFound();
                }
                var retval = new OperationState
                {
                    Id = id,
                };
                await GetOperationState(operationStatus, retval, id);
                retval.Message = operationStatus.StatusMessage;
                
                return Ok(retval);
            });
        }

        async Task GetOperationState(UpdateConfigStatus.ConfigUpdate operationStatus, OperationState state, string operationId)
        {
            switch (operationStatus.State)
            {
                case UpdateConfigStatus.States.NotStarted:
                    state.Status = "NotStarted";
                    break;

                case UpdateConfigStatus.States.CreatingAccounts:
                case UpdateConfigStatus.States.ImportingAccounts:
                case UpdateConfigStatus.States.PreServiceUpdate:
                    state.Status = "InProgress";
                    break;

                case UpdateConfigStatus.States.UpdatingService:
                    System.Diagnostics.Debug.Assert(!String.IsNullOrWhiteSpace(operationStatus.CloudServiceUpdateOperationId));
                    var accessResult = await GetRdfeToken();
                    System.Diagnostics.Debug.Assert(accessResult != null && !String.IsNullOrWhiteSpace(accessResult.AccessToken));
                    using (var serviceClient = await AzureService.GetServiceManagementClient(accessResult.AccessToken))
                    {
                        await ServiceUpdater.UpdateOperationStatus(serviceClient, operationStatus);
                        if (operationStatus.State != UpdateConfigStatus.States.UpdatingService)
                        {
                            await GetOperationState(operationStatus, state, operationId);
                        }
                        else
                        {
                            state.Status = "InProgress";
                        }
                    }
                    break;

                case UpdateConfigStatus.States.Completed:
                    state.Status = "Succeeded";
                    break;

                case UpdateConfigStatus.States.Failed:
                    state.Status = "Failed";
                    break;
            }
        }
    }
}