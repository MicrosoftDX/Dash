//     Copyright (c) Microsoft Corporation.  All rights reserved.

/// <reference path="../../scripts/_references.ts" />

module Dash.Management.Controller {
    "use strict";

    export class ConfigurationController {
        static $inject = ['$scope', '$rootScope', 'configurationService'];

        constructor(private $scope: Model.IDashManagementScope, $rootScope: Model.IDashManagementScope, configurationService: Service.ConfigurationService) {

            $scope.editSwitch = (item, discardChanges) => this.editSwitch(item, discardChanges);
            $scope.delete = (item) => this.delete(item);
            $scope.generateStorageKey = (item) => this.generateStorageKey(item);

            this.populate(configurationService.getResourceClass());

            this.buttonBarButtons = [
                new Model.ButtonBarButton("Save", true, () => this.update(configurationService.getResourceClass())),
                new Model.ButtonBarButton("Cancel", true, () => this.populate(configurationService.getResourceClass()))
            ];
            $rootScope.buttonBarButtons = this.buttonBarButtons;
        }

        public buttonBarButtons: Model.ButtonBarButton[]

        public editSwitch(editItem : Model.ConfigurationItem, discardChanges: boolean) : void {
            editItem.toggleEdit(discardChanges);
            this.$scope.configuration.editingInProgress = editItem.editing;
        }

        public delete(editItem: Model.ConfigurationItem) {
        }

        public generateStorageKey(editItem: Model.ConfigurationItem) {
            editItem.generateStorageKey();
        }

        private populate(resource: Service.IConfigurationResourceClass): void {
            this.$scope.loadingMessage = "Retrieving configuration from the Dash service...";
            this.$scope.configuration = resource.get(
                (results) => {
                    this.$scope.loadingMessage = "";
                },
                (err) => {
                    this.$scope.error = err.data;
                    this.$scope.loadingMessage = "";
                });
        }

        private update(resource: Service.IConfigurationResourceClass): void {
            this.$scope.loadingMessage = "Saving configuration to the Dash service...";
            resource.save(this.$scope.configuration, 
                (results) => {
                    this.$scope.loadingMessage = "";
                },
                (err) => {
                    this.$scope.error = err.data;
                    this.$scope.loadingMessage = "";
                });
        }
    }
} 