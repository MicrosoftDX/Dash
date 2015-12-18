//     Copyright (c) Microsoft Corporation.  All rights reserved.

/// <reference path="../../scripts/_references.ts" />

module Dash.Management.Controller {

    export class HomeController extends ADashControllerBase {
        static $inject = ['$scope', '$rootScope', 'adalAuthenticationService', '$location', '$route', 'updateService'];

        constructor($scope: Model.IDashManagementScope,
            $rootScope: Model.IDashManagementScope,
            adalAuthenticationService,
            $location: ng.ILocationService,
            $route: angular.route.IRouteService,
            private updateService: Service.UpdateService) {

            super($scope, $rootScope, adalAuthenticationService, $location);

            $scope.areUpdatesAvailable = false;
            $scope.updateBannerClass = "";

            this.setTitleForRoute($route.current);
            this.checkForUpdates();
        }

        public checkForUpdates(): void {
            if (this.updateService.updatesHaveBeenChecked) {
                this.$scope.areUpdatesAvailable = this.updateService.updatesAreAvailable;
                this.$scope.updateBannerClass = this.updateService.severityBannerClass;
            }
            this.updateService.checkForUpdates()
                .finally(() => {
                    this.$scope.areUpdatesAvailable = this.updateService.updatesAreAvailable;
                    this.$scope.updateBannerClass = this.updateService.severityBannerClass;
                });
        }
    }
} 