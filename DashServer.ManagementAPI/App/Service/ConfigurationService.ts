//     Copyright (c) Microsoft Corporation.  All rights reserved.

/// <reference path="../../scripts/_references.ts" />

module Dash.Management.Service {
    "use strict";

    export class ConfigurationService {
        static $inject = ['$http'];

        apiHost: string = '/api/configuration/';

        constructor(private $http : ng.IHttpService) { }

        public getItems(): ng.IHttpPromise<any> {
            return this.$http.get(this.apiHost);
        }

        public putItem(editItem): ng.IHttpPromise<any> {
            return this.$http.put(this.apiHost, editItem);
        }
    }
}