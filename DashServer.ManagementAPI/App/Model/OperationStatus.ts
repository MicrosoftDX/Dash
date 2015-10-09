//     Copyright (c) Microsoft Corporation.  All rights reserved.

/// <reference path="../../scripts/_references.ts" />

module Dash.Management.Model {

    // Need to keep structure in sync with DashServer.ManagementAPI.Models.OperationState in the WebAPI
    export class OperationStatus {
        public Id: string
        public Status: string
        public Message: string
    }
}
         