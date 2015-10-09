//     Copyright (c) Microsoft Corporation.  All rights reserved.

/// <reference path="../../scripts/_references.ts" />

module Dash.Management.Service {

    export class OperationStatusService {
        static $inject = ['$resource'];

        private resourceClass: ng.resource.IResourceClass<Model.OperationStatus>;

        constructor($resource: ng.resource.IResourceService) {

            this.resourceClass = $resource<Model.OperationStatus>('/api/operations/index/:operationId');
        }

        public getOperationStatus(operationId: string, namespaceAccount: string, success: Function, error?: Function): Model.OperationStatus {
            return this.resourceClass.get({
                    operationId: operationId,
                    storageConnectionStringMaster: namespaceAccount,
                },
                success,
                error);
        }
    }
}
 