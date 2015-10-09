//     Copyright (c) Microsoft Corporation.  All rights reserved.

/// <reference path="../../scripts/_references.ts" />

module Dash.Management.Model {

    export class ButtonBarButton {
        constructor(public displayText: string, $scope: Model.IDashManagementScope, enabledExpression: string, public doClick: Function, private imageUrl?: string) {
            this.enabled = false;
            $scope.$watch(enabledExpression, (newValue: boolean) => this.enabled = newValue);
        }

        public enabled: boolean;
    }
} 