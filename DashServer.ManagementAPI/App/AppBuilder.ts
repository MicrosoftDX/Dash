//     Copyright (c) Microsoft Corporation.  All rights reserved.

/// <reference path="../scripts/_references.ts" />

module Dash.Management {

    export class AppBuilder {

        private app: ng.IModule;

        constructor(name: string) {
            this.app = angular.module(name, [
                // Angular modules 
                "ngRoute",
                "ngResource",
                "ui.bootstrap",
                // ADAL
                'AdalAngular'
            ]);
            this.app.config(['$routeProvider', '$httpProvider', 'adalAuthenticationServiceProvider', 'authConfigServiceProvider',
                ($routeProvider: ng.route.IRouteProvider, $httpProvider: ng.IHttpProvider, adalProvider, authConfigProvider: Service.IAuthConfigServiceProvider) => {
                    $routeProvider
                        .when("/Home",
                        {
                            title: "Home",
                            controller: Controller.HomeController,
                            templateUrl: "/app/views/home.html",
                            caseInsensitiveMatch: true,
                        })
                        .when("/Configuration",
                        {
                            title: "Configuration",
                            controller: Controller.ConfigurationController,
                            templateUrl: "/App/Views/Configuration.html",
                            requireADLogin: true,
                            caseInsensitiveMatch: true,
                        })
                        .when("/Update",
                        {
                            title: "Software Upgrades",
                            controller: Controller.UpdateController,
                            templateUrl: "/App/Views/Update.html",
                            requireADLogin: true,
                            caseInsensitiveMatch: true,
                        })
                        .otherwise(
                        {
                            redirectTo: "/Home"
                        });
                    // Configure ADAL - we use the config values from the server.
                    // We cheat a bit here - the $http service is not yet available, but jQuery's $.ajax is - and we 
                    // need synchronous behavior so that other services/controllers don't try to acquire the adal service
                    // prior to us initializing it here
                    var authConfigService = authConfigProvider.$get();
                    authConfigService.getConfig((results: Model.IAuthConfig, status: string) => {
                        adalProvider.init(
                            {
                                tenant: results.Tenant,
                                clientId: results.ClientId,
                                cacheLocation: window.location.hostname === "localhost" ? "localStorage" : "", // enable this for IE, as sessionStorage does not work for localhost.
                            },
                            $httpProvider);
                    });
                }]);
            this.app.service('configurationService', Service.ConfigurationService);
            this.app.service('updateService', Service.UpdateService);
            this.app.service('operationStatusService', Service.OperationStatusService);
            this.app.directive('storageValidator', ['configurationService', (configurationService) => new Controller.StorageValidationDirective(configurationService)]);
            this.app.service('authConfigService', Service.AuthConfigService);
        }

        public start(): void {
            $(document).ready(() => {
                console.log("booting " + this.app.name);
                angular.bootstrap(document, [this.app.name]);
            });
        }
    }
}

