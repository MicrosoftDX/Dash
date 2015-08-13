//     Copyright (c) Microsoft Corporation.  All rights reserved.

/// <reference path="../../scripts/_references.ts" />

module Dash.Management.Controller {
    "use strict";

    export class StorageValidationDirective implements ng.IDirective {
        static directiveId = "StorageValidation";

        constructor(private configurationService: Service.ConfigurationService) {
        }

        //public require = "ngModel";
        public restrict = "A";
        public link = ($scope: ng.IScope, element: ng.IAugmentedJQuery, attrs: ng.IAttributes, ngModel) => {
            new StorageValidationInstance(this.configurationService, $scope, element, attrs);
        }
    }

    enum StorageValidationChangeType {
        AccountName,
        AccountKey,
    }

    class StorageValidationInstance {

        constructor(private configurationService: Service.ConfigurationService,
            $scope: ng.IScope,
            element: ng.IAugmentedJQuery,
            attrs: ng.IAttributes) {

            var options = {
                storageValidator: "",
                storageValidatorControls: "",
            };
            angular.extend(options, attrs);
            var bindingObject = eval("(" + options.storageValidator + ")");
            var boundControls = eval("(" + options.storageValidatorControls + ")");

            this.accountName = new BoundStorageAttribute($scope, bindingObject.accountName, boundControls.accountName, 
                (newValue: string, oldValue: string, current) => this.processChange(StorageValidationChangeType.AccountName, newValue, oldValue, current));
            this.accountKey = new BoundStorageAttribute($scope, bindingObject.accountKey, boundControls.accountKey,
                (newValue: string, oldValue: string, current) => this.processChange(StorageValidationChangeType.AccountKey, newValue, oldValue, current));
        }

        private processChange(changeKey: StorageValidationChangeType, newValue: string, oldValue: string, current: any) {
            // If this is the initial binding, we can skip
            var changeCtrl: ng.INgModelController;
            switch (changeKey) {
                case StorageValidationChangeType.AccountName:
                    changeCtrl = this.accountName.boundController;
                    break;

                case StorageValidationChangeType.AccountKey:
                    changeCtrl = this.accountKey.boundController;
                    break;
            }
            if (changeCtrl.$dirty) {
                var accountName = changeKey === StorageValidationChangeType.AccountName ? newValue : this.accountName.getBoundValue();
                var accountKey = changeKey === StorageValidationChangeType.AccountKey ? newValue : this.accountKey.getBoundValue();
                this.configurationService.getResourceClass().validate({
                        storageAccountName: accountName,
                        storageAccountKey: accountKey,
                    },
                    (data) => {
                        this.accountName.boundController.$setValidity(StorageValidationDirective.directiveId, data.ExistingStorageNameValid || data.NewStorageNameValid);
                        this.accountKey.boundController.$setValidity(StorageValidationDirective.directiveId, data.StorageKeyValid);
                    },
                    (err) => {
                        this.accountName.boundController.$setValidity(StorageValidationDirective.directiveId, false);
                        this.accountKey.boundController.$setValidity(StorageValidationDirective.directiveId, false);
                    });
            }
        }

        private accountName: BoundStorageAttribute;
        private accountKey: BoundStorageAttribute;
    }

    class BoundStorageAttribute {
        constructor(private $scope: ng.IScope, bindingExpr: string, boundController: string, watchFunction: (newValue: string, oldValue: string, scope: ng.IScope) => any) {
            this.bindingExpression = bindingExpr;
            this.boundController = this.$scope.$eval(boundController);
            this.$scope.$watch(this.bindingExpression, watchFunction);
        }

        public getBoundValue() {
            return this.$scope.$eval(this.bindingExpression);
        }

        public bindingExpression: string;
        public boundController: ng.INgModelController;
    }
} 