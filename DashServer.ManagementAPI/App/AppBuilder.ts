/* Copyright (c) Microsoft Corporation.  All rights reserved. */  
 
/// <reference path="../scripts/_references.ts" />

module Dash.Management {
    "use strict";
    export class AppBuilder {

        app: ng.IModule;

        constructor(name: string) {
            this.app = angular.module(name, [
                // Angular modules 
                "ngRoute",
                "ui.bootstrap",
                // ADAL
                'AdalAngular'
            ]);
            this.app.config(['$routeProvider', '$httpProvider', 'adalAuthenticationServiceProvider',
                ($routeProvider: ng.route.IRouteProvider, $httpProvider, adalProvider) => {
                    $routeProvider
                        .when("/Home",
                        {
                            controller: 'homeCtrl',
                            templateUrl: "/app/views/home.html",
                            caseInsensitiveMatch: true,
                        })
                        .when("/Configuration",
                        {
                            controller: Dash.Management.Controller.ConfigurationController,
                            templateUrl: "/App/Views/Configuration.html",
                            requireADLogin: true,
                            caseInsensitiveMatch: true,
                        })
                        .when("/Update",
                        {
                            controller: Dash.Management.Controller.UpdateController,
                            templateUrl: "/App/Views/Update.html",
                            requireADLogin: true,
                            caseInsensitiveMatch: true,
                        })
                        .otherwise(
                        {
                            redirectTo: "/Home"
                        });

                    adalProvider.init(
                    {
                        tenant: 'microsoft.com',
                        clientId: '3528d0b3-8502-44d5-bf1b-378488650187',
                        cacheLocation: 'localStorage', // enable this for IE, as sessionStorage does not work for localhost.
                    },
                    $httpProvider);

                }]);
            this.app.service('configurationService', Dash.Management.Service.ConfigurationService);
            this.app.service('updateService', Dash.Management.Service.UpdateService);
            this.app.controller('homeCtrl', Dash.Management.Controller.HomeController);
        }

        public start() {
            $(document).ready(() => {
                console.log("booting " + this.app.name);
                angular.bootstrap(document, [this.app.name]);
            });
        }
    }
}

