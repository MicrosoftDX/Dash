﻿//     Copyright (c) Microsoft Corporation.  All rights reserved.

/// <reference path="../../scripts/_references.ts" />

module Dash.Management.Service {
    "use strict";

    export class UpdateService {
        static $inject = ['$http', '$q'];

        apiHost: string = '/api/update/';

        constructor(private $http: ng.IHttpService, private $q: ng.IQService) { }

        public checkForUpdates(): ng.IPromise<any> {

            this.updatesHaveBeenChecked = true;
            return this.$http.get(this.apiHost)
                .success((results: any) => {
                    this.updatesAreAvailable = results.AvailableUpdate;
                    switch (results.HighestSeverity) {
                        case 'Critical':
                            this.severityBannerClass = 'alert-danger';
                            break;

                        case 'Important':
                            this.severityBannerClass = 'alert-warning'
                            break;

                        default:
                            this.severityBannerClass = 'alert-info';
                            break;
                    }
                })
                .error((err) => {
                    console.debug(err);
                })
                .finally(() => {
                    this.updatesHaveBeenChecked = true;
                })
        }

        public getAvailableUpdates(): ng.IPromise<Array<Model.VersionUpdate>> {
            return this.$http.get(this.apiHost + "Updates")
                .then((results: any) => {
                    return $.map(results.data, (update, index) => {
                        return new Model.VersionUpdate(update.Version, update.Severity, update.Description);
                    });
                })
        }

        public applyUpdate(version: string): ng.IHttpPromise<any> {
            return this.$http.post(this.apiHost + "Update", { version: version });
        }

        public updatesHaveBeenChecked: boolean = false;
        public updatesAreAvailable: boolean = false;
        public severityBannerClass: string = "";
    }
}
 