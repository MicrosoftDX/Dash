//     Copyright (c) Microsoft Corporation.  All rights reserved.

/// <reference path="../../scripts/_references.ts" />

module Dash.Management.Service {
    "use strict";

    export interface IConfigurationResourceClass extends ng.resource.IResourceClass<Model.Configuration> {

        validate(params: Object, success: Function, error?: Function): Model.Configuration;
    }

    export class ConfigurationService {
        static $inject = ['$resource'];

        private resourceClass: IConfigurationResourceClass;

        constructor($resource: ng.resource.IResourceService) {
            var tmp = 10;
            this.resourceClass = <IConfigurationResourceClass>$resource<Model.Configuration>(
                '/api/configuration/:action',
                null,
                {
                    get: {
                        method: 'GET',
                        isArray: false,
                        transformResponse: (results) => {
                            var responseObj = angular.fromJson(results);
                            if (responseObj["AccountSettings"] !== undefined) {
                                return new Model.Configuration(new Model.ConfigurationSettings(angular.fromJson(results)));
                            }
                            return responseObj;
                        }
                    },
                    save: {
                        method: 'PUT',
                        transformRequest: (data: Model.Configuration, headers) => {
                            return data.settings.toString();
                        }
                    },
                    validate: {
                        method: 'GET',
                        isArray: false,
                        params: {
                            action: 'validate',
                        },
                    }
            });
        }

        public getResourceClass(): IConfigurationResourceClass {
            return this.resourceClass;
        }
    }
}