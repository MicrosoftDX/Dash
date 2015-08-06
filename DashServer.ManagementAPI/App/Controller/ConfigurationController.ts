/// <reference path="../../scripts/_references.ts" />

module Dash.Management.Controller {
    "use strict";

    export class ConfigurationController {
        static $inject = ['$scope', 'configurationService'];

        constructor(private $scope: Dash.Management.Model.IDashManagementScope, private configurationService: Dash.Management.Service.ConfigurationService) {
            $scope.editSwitch = (item) => this.editSwitch(item);
            $scope.delete = (item) => this.delete(item);

            if (this.$scope.configuration === null || this.$scope.configuration === undefined) {
                this.$scope.configuration = new Dash.Management.Model.Configuration();
            }
            this.populate();
        }

        public editSwitch(editItem : Dash.Management.Model.ConfigurationItem) : void {
            editItem.toggleEdit();
            this.$scope.configuration.editingInProgress = editItem.editing;
        }

        public delete(editItem: Dash.Management.Model.ConfigurationItem) {
        }

        public populate(): void {
            this.$scope.configuration.loadingMessage = "Retrieving configuration from the Dash service...";
            this.configurationService.getItems()
                .success((results: any) => {
                    console.debug('Results ' + results);
                    // Project the response into something we can use to manage edit actions
                    this.$scope.configuration.settings = new Dash.Management.Model.ConfigurationSettings(results);
                    this.$scope.configuration.loadingMessage = "";
                })
                .error((err) => {
                    this.$scope.configuration.error = err;
                    this.$scope.configuration.loadingMessage = "";
                    this.$scope.configuration.settings = null;
                });
        }

        public update(editItem) : void {
            this.configurationService.putItem(editItem)
                .success((results) => {
                    this.$scope.configuration.loadingMessage = "";
                    this.populate();
                    this.editSwitch(editItem);
                })
                .error((err) => {
                    this.$scope.configuration.error = err;
                    this.$scope.configuration.loadingMessage = "";
                });
        }
    }
} 