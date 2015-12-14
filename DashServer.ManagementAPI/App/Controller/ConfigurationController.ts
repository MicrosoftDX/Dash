//     Copyright (c) Microsoft Corporation.  All rights reserved.

/// <reference path="../../scripts/_references.ts" />

module Dash.Management.Controller {

    export class ConfigurationController {
        static $inject = ['$scope', '$rootScope', '$timeout', 'configurationService', '$location', 'operationStatusService'];

        constructor(private $scope: Model.IDashManagementScope,
            $rootScope: Model.IDashManagementScope,
            private $timeout: ng.ITimeoutService,
            configurationService: Service.ConfigurationService,
            private $location: ng.ILocationService,
            private operationStatusService: Service.OperationStatusService) {

            $scope.addAccount = () => this.addAccount();
            $scope.deleteAccount = (item) => this.deleteAccount(item);
            $scope.generateStorageKey = (item) => this.generateStorageKey(item);
            $rootScope.isControllerActive = (loc) => this.isActive(loc);

            this.buttonBarButtons = [
                new Model.ButtonBarButton("Commit", $scope, "!updateInProgress", () => this.update(configurationService.getResourceClass())),
                new Model.ButtonBarButton("Revert", $scope, "!updateInProgress", () => this.populate(configurationService.getResourceClass()))
            ];
            $scope.buttonBarButtons = this.buttonBarButtons;

            this.populate(configurationService.getResourceClass());
        }

        public isActive(viewLocation): boolean {
            return viewLocation === this.$location.path();
        }

        public buttonBarButtons: Model.ButtonBarButton[]

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
                (err) => {
                    this.setMessage(true, err.data);
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
                (err) => {
                    this.setMessage(true, err.data);
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
                        this.setMessage(failure, failure ? status.Message : "Configuration update completed successfully");
                        this.setUpdateState(false);
                    }
                },
                (err) => {
                    this.setMessage(true, err.data);
                    this.setUpdateState(false);
                });
        }

        private setMessage(error: boolean, message: string): void {
            this.$scope.error_class = error ? "alert-danger" : "alert-info";
            this.$scope.error = message;
            this.$scope.loadingMessage = "";
        }

        private setUpdateState(updateInProgress: boolean): void {
            this.$scope.updateInProgress = updateInProgress;
        }
    }
} 