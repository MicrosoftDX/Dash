//     Copyright (c) Microsoft Corporation.  All rights reserved.

/// <reference path="../../scripts/_references.ts" />

module Dash.Management.Service {

    export interface IAuthConfigServiceProvider extends ng.IServiceProvider {

        $get() : IAuthConfigService;
    }

    export interface IAuthConfigService {

        getConfig(success: (data: Model.IAuthConfig, textStatus: string) => any);
    }

    export class AuthConfigService implements IAuthConfigService {

        constructor() { }

        public getConfig(success: (data: Model.IAuthConfig, textStatus: string) => any): void {

            // Use JQuery here so that we can make this a synchronous call
            $.ajax({
                url: "/api/authconfig",
                async: false,
                success: success
            });
        }
    }
}
 