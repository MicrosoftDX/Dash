/// <reference path="../../scripts/_references.ts" />

module Dash.Management.Controller {
    "use strict";

    export class UpdateController {
        static $inject = ['$scope', 'updateService'];

        constructor(private $scope: Dash.Management.Model.IDashManagementScope, private updateService: Dash.Management.Service.UpdateService) {

            $scope.availableUpdates = null;
        }

        public applyUpdate(version: string) {
            this.$scope.updateInProgress = true;
            this.$scope.updateMessage = "Updating DASH service to version: " + version;
        //if (confirm('You have selected to upgrade this Management Console site to version: ' + version + '\n\nThis operation cannot be undone.\n\nAre you sure you want to continue?')) {
            this.updateService.applyUpdate(version)
                .success((results: any) => {
                    this.$scope.updateMessage = "DASH service has been successfully updated to version: " + version;
                })
                .error((err: any) => {
                    this.$scope.updateMessage = "An error occurred updating the DASH service. Details: " + err;
                })
                .finally(() => {
                    this.$scope.updateInProgress = false;
                });
        }
    }
}
 