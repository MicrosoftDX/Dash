//     Copyright (c) Microsoft Corporation.  All rights reserved.

/// <reference path="../../scripts/_references.ts" />

module Dash.Management.Service {

    export interface IConfigurationResourceClass extends ng.resource.IResourceClass<Model.Configuration> {

        validate(params: Object, success: Function, error?: Function): Model.Configuration;
    }

    export class ConfigurationService {
        static $inject = ['$resource'];

        private resourceClass: IConfigurationResourceClass;

        constructor($resource: ng.resource.IResourceService) {

            this.resourceClass = <IConfigurationResourceClass>$resource<Model.Configuration>(
                '/configuration/:action',
                null,
                {
                    get: {
                        method: 'GET',
                        isArray: false,
                        transformResponse: (results) => ConfigurationService.getModelFromResponse(results),
                    },
                    save: {
                        method: 'PUT',
                        transformResponse: (results) => ConfigurationService.getModelFromResponse(results),
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

        static getModelFromResponse(response): any {
            var responseObj = angular.fromJson(response);
            if (responseObj["AccountSettings"] !== undefined) {
                return new Model.Configuration(new Model.ConfigurationSettings(responseObj), responseObj.OperationId);
            }
            return responseObj;
        }

        public getResourceClass(): IConfigurationResourceClass {
            return this.resourceClass;
        }
    }
}