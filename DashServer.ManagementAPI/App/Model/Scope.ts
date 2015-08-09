//     Copyright (c) Microsoft Corporation.  All rights reserved.

/// <reference path="../../scripts/_references.ts" />

module Dash.Management.Model {
    "use strict";

    export interface IDashManagementScope extends ng.IScope {
        error: string
        loadingMessage: string
        configuration: Configuration
        login: Function
        logout: Function
        isControllerActive: Function
        editSwitch: Function
        delete: Function
        generateStorageKey: Function
        isEditStyle: Function
        notifyChange: Function
        areUpdatesAvailable: boolean
        updateBannerClass: string
        updateInProgress: boolean
        updateMessage: string
        availableUpdates: VersionUpdate[]
        getHtmlDescription: Function
        applyUpdate: Function
        buttonBarButtons: ButtonBarButton[]
    }
} 

