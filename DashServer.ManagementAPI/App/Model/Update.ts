//     Copyright (c) Microsoft Corporation.  All rights reserved.

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

        constructor(public versionString: string, severity: number, public description: string) {

            switch (severity) {
                case 3:
                    this.severity = 'Critical';
                    this.severityClass = 'alert-danger';
                    break;

                case 2:
                    this.severity = 'Important';
                    this.severityClass = 'alert-warning';
                    break;

                default:
                    this.severity = 'Optional';
                    this.severityClass = 'alert-info';
                    break;
            }
        }

        public severity: string;
        public severityClass: string;
    }
} 