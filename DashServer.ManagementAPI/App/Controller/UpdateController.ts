//     Copyright (c) Microsoft Corporation.  All rights reserved.

/// <reference path="../../scripts/_references.ts" />

module Dash.Management.Controller {

    export class UpdateController extends ADashControllerBase {
        static $inject = ['$scope', '$rootScope', 'adalAuthenticationService', '$location', 'updateService', '$sce'];

        constructor($scope: Model.IDashManagementScope,
            $rootScope: Model.IDashManagementScope,
            adalAuthenticationService,
            $location: ng.ILocationService,
            private updateService: Service.UpdateService,
            private $sce: ng.ISCEService) {

            super($scope, $rootScope, adalAuthenticationService, $location);

            $scope.getHtmlDescription = (update: Model.VersionUpdate) => this.getHtmlDescription(update);
            $scope.applyUpdate = (update: Model.VersionUpdate) => this.applyUpdate(update);

            $scope.loadingMessage = "Retrieving available versions for the Dash service...";
            $scope.availableUpdates = new Model.AvailableUpdates();

            this.getAvailableUpdates(true);
        }

        public getAvailableUpdates(clearLoadingMessage: boolean): void {
            this.updateService.getAvailableUpdates()
                .then((versions: Model.AvailableUpdates) => {
                    this.$scope.availableUpdates = versions;
                })
                .catch((err: ng.IHttpPromiseCallbackArg<any>) => {
                    this.setError(true, err.data, err.headers);
                })
                .finally(() => {
                    if (clearLoadingMessage) {
                        this.$scope.loadingMessage = "";
                    }
                });
        }

        public applyUpdate(version: Model.VersionUpdate): void {
            if (confirm('You have selected to upgrade this DASH server to version: ' + version.versionString + '\n\nThis operation cannot be undone.\n\nAre you sure you want to continue?')) {
                this.setUpdateState(true);
                this.$scope.loadingMessage = "Updating DASH service to version: " + version.versionString;
                this.updateService.applyUpdate(version.versionString)
                    .then((results: ng.IHttpPromiseCallbackArg<any>) => {
                        this.$scope.loadingMessage = "The DASH service is being updated to version: " + version.versionString + ". The request id: " + results.data.OperationId;
                    },
                    (err: ng.IHttpPromiseCallbackArg<string>) => {
                        this.setError(true, err.data || err.statusText, err.headers);
                    })
                    .finally(() => {
                        this.setUpdateState(false);
                        this.getAvailableUpdates(false);
                    });
            }
        }

        public getHtmlDescription(update: Model.VersionUpdate): void {
            return this.$sce.trustAsHtml(update.description);
        }
    }
}
 