/// <reference path="../../scripts/_references.ts" />

module Dash.Management.Model {
    "use strict";

    export interface IDashManagementScope extends ng.IScope {
        configuration: Configuration
        login: Function
        logout: Function
        isControllerActive: Function
        editSwitch: Function
        delete: Function
        areUpdatesAvailable: boolean
        updateBannerClass: string
        updateInProgress: boolean
        updateMessage: string
        availableUpdates: VersionUpdate[]
    }
} 

