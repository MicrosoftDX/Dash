//     Copyright (c) Microsoft Corporation.  All rights reserved.

/// <reference path="../../scripts/_references.ts" />

module Dash.Management.Controller {
    "use strict";

    export class HomeController {
        static $inject = ['$scope', '$rootScope', 'adalAuthenticationService', '$location', 'updateService'];

        constructor(private $scope: Model.IDashManagementScope,
            $rootScope: Model.IDashManagementScope,
            private adalAuthenticationService,
            private $location: ng.ILocationService,
            private updateService: Service.UpdateService) {

            $rootScope.login = () => this.login();
            $rootScope.logout = () => this.logout();
            $rootScope.isControllerActive = (loc) => this.isActive(loc);
            $rootScope.buttonBarButtons = [];

            $scope.areUpdatesAvailable = false;
            $scope.updateBannerClass = "";

            this.checkForUpdates();
        }

        public login() {
            this.adalAuthenticationService.login();
        }

        public logout() {
            this.adalAuthenticationService.logOut();
        }

        public isActive(viewLocation): boolean {
            return viewLocation === this.$location.path();
        }

        public checkForUpdates() {
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