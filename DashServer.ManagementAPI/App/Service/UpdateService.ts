/// <reference path="../../scripts/_references.ts" />

module Dash.Management.Service {
    "use strict";

    export class UpdateService {
        static $inject = ['$http'];

        apiHost: string = '/api/updates/';

        constructor(private $http: ng.IHttpService) { }

        public checkForUpdates(): ng.IPromise<any> {

            return this.$http.get(this.apiHost)
                .success((results: any) => {
                    this.updatesAreAvailable = results.UpdatesAvailable;
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
                .finally(() => {
                    this.updatesHaveBeenChecked = true;
                })
        }

        public applyUpdate(version: string): ng.IHttpPromise<any> {
            return this.$http.post(this.apiHost, { version: version });
        }

        public updatesHaveBeenChecked: boolean = false;
        public updatesAreAvailable: boolean = false;
        public severityBannerClass: string = "";
    }
}
 