//     Copyright (c) Microsoft Corporation.  All rights reserved.

/// <reference path="../../scripts/_references.ts" />

module Dash.Management.Controller {
    "use strict";

    export class ConfigurationController {
        static $inject = ['$scope', '$rootScope', '$timeout', 'configurationService', 'operationStatusService'];

        constructor(private $scope: Model.IDashManagementScope,
            $rootScope: Model.IDashManagementScope,
            private $timeout: ng.ITimeoutService,
            configurationService: Service.ConfigurationService,
            private operationStatusService: Service.OperationStatusService) {

            $scope.editSwitch = (item, discardChanges) => this.editSwitch(item, discardChanges);
            $scope.addAccount = () => this.addAccount();
            $scope.deleteAccount = (item) => this.deleteAccount(item);
            $scope.generateStorageKey = (item) => this.generateStorageKey(item);

            this.buttonBarButtons = [
                new Model.ButtonBarButton("Save", $scope, "!updateInProgress && !configuration.editingInProgress", () => this.update(configurationService.getResourceClass())),
                new Model.ButtonBarButton("Cancel", $scope, "!updateInProgress", () => this.populate(configurationService.getResourceClass()))
            ];
            $rootScope.buttonBarButtons = this.buttonBarButtons;

            this.populate(configurationService.getResourceClass());
        }

        public buttonBarButtons: Model.ButtonBarButton[]

        public editSwitch(editItem : Model.ConfigurationItem, discardChanges: boolean) : void {
            editItem.toggleEdit(discardChanges);
            this.$scope.configuration.editingInProgress = editItem.editing;
        }

        public addAccount() {
            var newAccount = Model.StorageConnectionItem.createScaleOutAccount("", true);
            this.$scope.configuration.settings.scaleOutStorage.accounts.push(newAccount);
            this.editSwitch(newAccount, false);
        }

        public deleteAccount(deleteItem: Model.ConfigurationItem) {
            this.$scope.configuration.settings.scaleOutStorage.accounts.splice(
                this.$scope.configuration.settings.scaleOutStorage.accounts.indexOf(<Model.StorageConnectionItem>deleteItem), 1);
        }

        public generateStorageKey(editItem: Model.ConfigurationItem) {
            editItem.generateStorageKey();
        }

        private populate(resource: Service.IConfigurationResourceClass): void {
            this.setUpdateState(true);
            this.$scope.loadingMessage = "Retrieving configuration from the Dash service...";
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
                    this.$scope.error = err.data;
                    this.$scope.loadingMessage = "";
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
                    this.$scope.error = err.data;
                    this.$scope.loadingMessage = "";
                    this.setUpdateState(false);
                });
        }

        private updateOperationStatus(operationId: string) {
            var namespaceAccount = this.$scope.configuration.settings.specialSettings.namespaceStorage.getValue();9
            this.operationStatusService.getOperationStatus(operationId, namespaceAccount,
                (status: Model.OperationStatus) => {
                    this.$scope.loadingMessage = "Updating service: " + status.Status + ": " + status.Message;
                    if (status.Status != "Succeeded" && status.Status != "Failed") {
                        this.$timeout(() => this.updateOperationStatus(operationId), 10000);
                    }
                    else {
                        this.setUpdateState(false);
                    }
                },
                (err) => {
                    this.$scope.error = err.data;
                    this.$scope.loadingMessage = "";
                    this.setUpdateState(false);
                });
        }

        private setUpdateState(updateInProgress: boolean) {
            this.$scope.updateInProgress = updateInProgress;
        }
    }
} 