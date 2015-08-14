//     Copyright (c) Microsoft Corporation.  All rights reserved.

using System;
using System.Threading.Tasks;
using System.Web.Http;
using Microsoft.Dash.Common.OperationStatus;
using DashServer.ManagementAPI.Models;
using Microsoft.Dash.Common.ServiceManagement;
using Microsoft.Dash.Common.Processors;

namespace DashServer.ManagementAPI.Controllers
{
    [Authorize]
    public class OperationsController : DelegatedAuthController
    {
        [HttpGet, ActionName("Index")]
        public async Task<IHttpActionResult> Get(string operationId)
        {
            return await DoActionAsync("OperationsController.Get", async () =>
            {
                var operationStatus = await UpdateConfigStatus.GetConfigUpdateStatus(operationId);
                if (operationStatus.State == UpdateConfigStatus.States.Unknown)
                {
                    return NotFound();
                }
                var retval = new OperationState
                {
                    Id = operationId,
                    Message = operationStatus.StatusMessage,
                };
                await GetOperationState(operationStatus, retval, operationId);
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
                    state.Status = "InProgress";
                    break;

                case UpdateConfigStatus.States.UpdatingService:
                    System.Diagnostics.Debug.Assert(!String.IsNullOrWhiteSpace(operationStatus.CloudServiceUpdateOperationId));
                    using (var serviceClient = await AzureService.GetServiceManagementClient((await GetRdfeToken()).AccessToken))
                    {
                        await ServiceUpdater.UpdateOperationStatus(serviceClient, operationStatus, operationId);
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