﻿//     Copyright (c) Microsoft Corporation.  All rights reserved.

/// <reference path="../../scripts/_references.ts" />

module Dash.Management.Controller {

    export class UpdateController {
        static $inject = ['$scope', '$rootScope', 'updateService', '$sce', '$location'];

        constructor(private $scope: Model.IDashManagementScope, $rootScope: Model.IDashManagementScope, private updateService: Service.UpdateService, private $sce: ng.ISCEService, private $location: ng.ILocationService) {

            $scope.getHtmlDescription = (update: Model.VersionUpdate) => this.getHtmlDescription(update);
            $scope.applyUpdate = (update: Model.VersionUpdate) => this.applyUpdate(update);

            $scope.loadingMessage = "Retrieving available versions for the Dash service...";
            $scope.error = "";
            $scope.availableUpdates = new Model.AvailableUpdates();
            $rootScope.isControllerActive = (loc) => this.isActive(loc);

            $rootScope.buttonBarButtons = [];

            this.getAvailableUpdates(true);
        }

        public isActive(viewLocation): boolean {
            return viewLocation === this.$location.path();
        }

        public getAvailableUpdates(clearLoadingMessage: boolean): void {
            this.updateService.getAvailableUpdates()
                .then((versions: Model.AvailableUpdates) => {
                    this.$scope.availableUpdates = versions;
                })
                .catch((err) => {
                    this.setError(true, err);
                })
                .finally(() => {
                    if (clearLoadingMessage) {
                        this.$scope.loadingMessage = "";
                    }
                });
        }

        public applyUpdate(version: Model.VersionUpdate): void {
            if (confirm('You have selected to upgrade this DASH server to version: ' + version.versionString + '\n\nThis operation cannot be undone.\n\nAre you sure you want to continue?')) {
                this.$scope.updateInProgress = true;
                this.$scope.loadingMessage = "Updating DASH service to version: " + version.versionString;
                this.updateService.applyUpdate(version.versionString)
                    .success((results: any) => {
                        this.$scope.loadingMessage = "The DASH service is being updated to version: " + version.versionString + ". The request id: " + results.RequestId;
                    })
                    .error((err: any) => {
                        this.setError(true, err);
                    })
                    .finally(() => {
                        this.$scope.updateInProgress = false;
                        this.getAvailableUpdates(false);
                    });
            }
        }

        public getHtmlDescription(update: Model.VersionUpdate): void {
            return this.$sce.trustAsHtml(update.description);
        }

        private setError(error: boolean, message: string): void {
            this.$scope.error_class = error ? "alert-danger" : "alert-info";
            this.$scope.error = message;
            this.$scope.loadingMessage = "";
        }
    }
}
 