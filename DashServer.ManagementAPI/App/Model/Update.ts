/// <reference path="../../scripts/_references.ts" />

module Dash.Management.Model {
    "use strict";

    export class UpdateModel {

        constructor() {
            this.availableUpdates = false;
            this.highestSeverity = "";
        }

        public availableUpdates: boolean
        public highestSeverity: string
    }

    export class VersionUpdate {

        constructor(public versionString: string, public severity: string, public description: string) {

            switch (severity) {
                case 'Critical':
                    this.severityClass = 'alert-danger';
                    break;

                case 'Important':
                    this.severityClass = 'alert-warning'
                    break;

                default:
                    this.severityClass = 'alert-info';
                    break;
            }
        }

        public severityClass: string;
    }
} 