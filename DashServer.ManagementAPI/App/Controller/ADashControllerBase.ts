//     Copyright (c) Microsoft Corporation.  All rights reserved.

/// <reference path="../../scripts/_references.ts" />

module Dash.Management.Controller {

    export class ADashControllerBase {

        constructor(protected $scope: Model.IDashManagementScope,
            protected $rootScope: Model.IDashManagementScope,
            protected adalAuthenticationService,
            protected $location: ng.ILocationService) {

            $rootScope.login = () => this.login();
            $rootScope.logout = () => this.logout();
            $rootScope.isControllerActive = (location) => this.isActive(location);
            $rootScope.buttonBarButtons = [];

            $scope.$on('$routeChangeSuccess', (event, current, previous) => this.setTitleForRoute(current.$$route));
            $scope.loadingMessage = "";
            $scope.error = "";
        }

        public login(): void {
            this.adalAuthenticationService.login();
        }

        public logout(): void {
            this.adalAuthenticationService.logOut();
        }

        public acquireTokenForResource(resource: string) {
            this.adalAuthenticationService.login(resource);
        }

        public isActive(viewLocation): boolean {
            return viewLocation === this.$location.path();
        }

        public setTitleForRoute(route: angular.route.IRoute): void {
            this.$rootScope.title = "DASH Management - " + route.name;
        }

        protected setError(error: boolean, message: any, responseHeaders: ng.IHttpHeadersGetter): void {
            var acquireMfaResource = "";
            if (responseHeaders != null) {
                // If we received a 401 error with WWW-Authenticate response headers, we may need to 
                // re-authenticate to satisfy 2FA requirements for underlying services used by the WebAPI
                // (eg. RDFE). In that case, we need to explicitly specify the name of the resource we
                // want 2FA authentication to.
                var wwwAuth = responseHeaders("www-authenticate");
                if (wwwAuth) {
                    // Handle the multiple www-authenticate headers case
                    angular.forEach(wwwAuth.split(","), (authScheme: string, index: number) => {
                        var paramsDelim = authScheme.indexOf(" ");
                        if (paramsDelim != -1) {
                            var params = authScheme.substr(paramsDelim + 1);
                            var paramsValues = params.split("=");
                            if (paramsValues[0] === "interaction_required") {
                                acquireMfaResource = paramsValues[1];
                            }
                        }
                    });
                }
            }
            if (acquireMfaResource) {
                // TODO: When we have a mechanism in adal to re-authenticate to a specific resource, applying
                // the MFA policies of that resource, we'll hit that. In the interim, the user will have to manually
                // work around
                //this.acquireTokenForResource(acquireMfaResource)
                message = (message || "") + (message ? " - " : "") + "Microsoft Azure requires Two Factor Authentication to manipulate your service configuration. " +
                    "Please use another application (eg. Azure Portal) to enforce 2FA and then re-logon to this application.";
            }
            if ($.isPlainObject(message)) {
                message = $.map(["Message", "ExceptionMessage", "ExceptionType"], (attributeName) => message[attributeName])
                    .join(" - ");
            }
            this.$scope.error_class = error ? "alert-danger" : "alert-info";
            this.$scope.error = message;
            this.$scope.loadingMessage = "";
        }

        protected setUpdateState(updateInProgress: boolean): void {
            this.$scope.updateInProgress = updateInProgress;
        }
    }
} 