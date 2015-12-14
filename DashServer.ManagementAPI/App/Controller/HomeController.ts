//     Copyright (c) Microsoft Corporation.  All rights reserved.

/// <reference path="../../scripts/_references.ts" />

module Dash.Management.Controller {

    export class HomeController {
        static $inject = ['$scope', '$rootScope', 'adalAuthenticationService', '$location', 'updateService'];

        constructor(private $scope: Model.IDashManagementScope,
            private $rootScope: Model.IDashManagementScope,
            private adalAuthenticationService,
            private $location: ng.ILocationService,
            private updateService: Service.UpdateService) {

            $rootScope.login = () => this.login();
            $rootScope.logout = () => this.logout();
            $rootScope.isControllerActive = (loc) => this.isActive(loc);
            $rootScope.buttonBarButtons = [];
            $rootScope.$on('$routeChangeSuccess', (event, current, previous) => this.setTitleForRoute(current));

            $scope.areUpdatesAvailable = false;
            $scope.updateBannerClass = "";

            this.checkForUpdates();
        }

        public login(): void {
            this.adalAuthenticationService.login();
        }

        public logout(): void {
            this.adalAuthenticationService.logOut();
        }

        public isActive(viewLocation): boolean {
            return viewLocation === this.$location.path();
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

        public setTitleForRoute(current): void {
            this.$rootScope.title = "DASH Management - " + current.$$route.title;
        }
    }
} 