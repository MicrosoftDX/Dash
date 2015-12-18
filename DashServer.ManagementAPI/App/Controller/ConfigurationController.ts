//     Copyright (c) Microsoft Corporation.  All rights reserved.

/// <reference path="../../scripts/_references.ts" />

module Dash.Management.Controller {

    export class ConfigurationController extends ADashControllerBase {
        static $inject = ['$scope', '$rootScope', 'adalAuthenticationService', '$location', '$timeout', 'configurationService', 'operationStatusService'];

        constructor($scope: Model.IDashManagementScope,
            $rootScope: Model.IDashManagementScope,
            adalAuthenticationService,
            $location: ng.ILocationService,
            private $timeout: ng.ITimeoutService,
            configurationService: Service.ConfigurationService,
            private operationStatusService: Service.OperationStatusService) {

            super($scope, $rootScope, adalAuthenticationService, $location);

            $scope.addAccount = () => this.addAccount();
            $scope.deleteAccount = (item) => this.deleteAccount(item);
            $scope.generateStorageKey = (item) => this.generateStorageKey(item);

            $scope.buttonBarButtons = [
                new Model.ButtonBarButton("Commit", $scope, "configurationForm.$valid && !updateInProgress", () => this.update(configurationService.getResourceClass()), true),
                new Model.ButtonBarButton("Revert", $scope, "!updateInProgress", () => this.populate(configurationService.getResourceClass()), false)
            ];
            $scope.updateConfiguration = () => this.update(configurationService.getResourceClass());

            this.populate(configurationService.getResourceClass());
        }

        public addAccount() : void {
            var newAccount = Model.StorageConnectionItem.createScaleOutAccount("", true);
            this.$scope.configuration.settings.scaleOutStorage.accounts.push(newAccount);
        }

        public deleteAccount(deleteItem: Model.ConfigurationItem) : void {
            this.$scope.configuration.settings.scaleOutStorage.accounts.splice(
                this.$scope.configuration.settings.scaleOutStorage.accounts.indexOf(<Model.StorageConnectionItem>deleteItem), 1);
        }

        public generateStorageKey(editItem: Model.ConfigurationItem): void {
            editItem.generateStorageKey();
        }

        private populate(resource: Service.IConfigurationResourceClass): void {
            this.setUpdateState(true);
            this.$scope.loadingMessage = "Retrieving configuration from the Dash service...";
            this.$scope.error = "";
            this.$scope.configuration = resource.get(
                (results) => {
                    var updateInProgress: boolean = this.$scope.configuration.operationId != null;
                    this.setUpdateState(updateInProgress);
                    if (updateInProgress) {
                        this.updateOperationStatus(this.$scope.configuration.operationId);
                    }
                    else {
                        this.$scope.loadingMessage = "";
                    }
                },
                (err: ng.IHttpPromiseCallbackArg<any>) => {
                    this.setError(true, err.data, err.headers);
                });
        }

        private update(resource: Service.IConfigurationResourceClass): void {
            this.$scope.loadingMessage = "Saving configuration to the Dash service...";
            this.setUpdateState(true);
            resource.save(this.$scope.configuration, 
                (results) => {
                    this.$scope.configuration = results;
                    this.updateOperationStatus(results.operationId);
                },
                (err: ng.IHttpPromiseCallbackArg<any>) => {
                    this.setError(true, err.data, err.headers);
                    this.setUpdateState(false);
                });
        }

        private updateOperationStatus(operationId: string): void {
            var namespaceAccount = this.$scope.configuration.settings.specialSettings.namespaceStorage.getValue();
            this.operationStatusService.getOperationStatus(operationId, namespaceAccount,
                (status: Model.OperationStatus) => {
                    this.$scope.loadingMessage = "Updating service: " + status.Status + ": " + status.Message;
                    if (status.Status != "Succeeded" && status.Status != "Failed") {
                        this.$timeout(() => this.updateOperationStatus(operationId), 10000);
                    }
                    else {
                        var failure = status.Status == "Failed";
                        this.setError(failure, failure ? status.Message : "Configuration update completed successfully", null);
                        this.setUpdateState(false);
                    }
                },
                (err: ng.IHttpPromiseCallbackArg<any>) => {
                    this.setError(true, err.data, err.headers);
                    this.setUpdateState(false);
                });
        }
    }
} 