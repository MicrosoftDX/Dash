//     Copyright (c) Microsoft Corporation.  All rights reserved.

/// <reference path="../../scripts/_references.ts" />

module Dash.Management.Controller {
    "use strict";

    export class ConfigurationController {
        static $inject = ['$scope', '$rootScope', 'configurationService'];

        constructor(private $scope: Model.IDashManagementScope, $rootScope: Model.IDashManagementScope, private configurationService: Dash.Management.Service.ConfigurationService) {
            $scope.editSwitch = (item, discardChanges) => this.editSwitch(item, discardChanges);
            $scope.delete = (item) => this.delete(item);
            $scope.generateStorageKey = (item) => this.generateStorageKey(item);
            $scope.isEditStyle = this.isEditStyle;
            $scope.notifyChange = (item) => this.notifyChange(item);

            this.buttonBarButtons = [
                new Model.ButtonBarButton("Save", false),
                new Model.ButtonBarButton("Cancel", false)
            ];
            $rootScope.buttonBarButtons = this.buttonBarButtons;

            this.$scope.configuration = new Model.Configuration();
            this.populate();
        }

        public buttonBarButtons: Model.ButtonBarButton[]

        public editSwitch(editItem : Model.ConfigurationItem, discardChanges: boolean) : void {
            if (editItem.toggleEdit(discardChanges)) {
                this.enableButtons();
            }
            this.$scope.configuration.editingInProgress = editItem.editing;
        }

        public delete(editItem: Model.ConfigurationItem) {
        }

        public generateStorageKey(editItem: Model.ConfigurationItem) {
            editItem.generateStorageKey();
            this.enableButtons();
        }

        public isEditStyle(item: Model.ConfigurationItem, style: Model.EditorStyles): boolean {
            return (item.editorStyles & style) != 0;
        }

        public notifyChange(item: Model.ConfigurationItem) {
            if (!this.isEditStyle(item, Model.EditorStyles.EditMode)) {
                if (item.commitChanges()) {
                    this.enableButtons();
                }
            }
        }

        public populate(): void {
            this.$scope.loadingMessage = "Retrieving configuration from the Dash service...";
            this.configurationService.getItems()
                .success((results: any) => {
                    console.debug('Results ' + results);
                    // Project the response into something we can use to manage edit actions
                    this.$scope.configuration.settings = new Dash.Management.Model.ConfigurationSettings(results);
                    this.$scope.loadingMessage = "";
                })
                .error((err) => {
                    this.$scope.error = err;
                    this.$scope.loadingMessage = "";
                    this.$scope.configuration.settings = null;
                });
        }

        public update(editItem) : void {
            this.configurationService.putItem(editItem)
                .success((results) => {
                    this.$scope.loadingMessage = "";
                    this.populate();
                })
                .error((err) => {
                    this.$scope.error = err;
                    this.$scope.loadingMessage = "";
                });
        }

        private enableButtons() {
            this.buttonBarButtons.forEach((value, index) => value.enabled = true);
        }
    }
} 