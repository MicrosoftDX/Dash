//     Copyright (c) Microsoft Corporation.  All rights reserved.

/// <reference path="../../scripts/_references.ts" />

module Dash.Management.Model {

    export interface IDashManagementScope extends ng.IScope {
        title: string
        error: string
        error_class: string
        loadingMessage: string
        configuration: Model.Configuration
        login: Function
        logout: Function
        isControllerActive: Function
        addAccount: Function
        deleteAccount: Function
        generateStorageKey: Function
        areUpdatesAvailable: boolean
        updateBannerClass: string
        updateInProgress: boolean
        updateMessage: string
        availableUpdates: AvailableUpdates
        getHtmlDescription: Function
        applyUpdate: Function
        buttonBarButtons: ButtonBarButton[]
    }
} 

