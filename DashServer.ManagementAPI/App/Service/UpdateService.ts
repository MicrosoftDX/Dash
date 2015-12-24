//     Copyright (c) Microsoft Corporation.  All rights reserved.

/// <reference path="../../scripts/_references.ts" />

module Dash.Management.Service {

    export class UpdateService {
        static $inject = ['$http', '$q'];
        static apiHost = '/update/';

        constructor(private $http: ng.IHttpService, private $q: ng.IQService) { }

        public checkForUpdates(): ng.IPromise<any> {

            this.updatesHaveBeenChecked = true;
            return this.$http.get(UpdateService.apiHost + "available")
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

        public getAvailableUpdates(): ng.IPromise<Model.AvailableUpdates> {
            return this.$http.get(UpdateService.apiHost)
                .then((results: any) => {
                    var updates = new Model.AvailableUpdates();
                    updates.currentVersion = results.data.CurrentVersion;
                    updates.availableUpdates = $.map(results.data.AvailableUpdates, (update, index) => {
                        return new Model.VersionUpdate(update.Version, update.Severity, update.Description);
                    });
                    return updates;
                })
        }

        public applyUpdate(version: string): ng.IHttpPromise<any> {
            return this.$http.post(UpdateService.apiHost, { Version: version });
        }

        public updatesHaveBeenChecked: boolean = false;
        public updatesAreAvailable: boolean = false;
        public severityBannerClass: string = "";
    }
}
 