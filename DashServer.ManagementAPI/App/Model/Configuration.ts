//     Copyright (c) Microsoft Corporation.  All rights reserved.

/// <reference path="../../scripts/_references.ts" />

module Dash.Management.Model {
    "use strict";

    export class Configuration {

        constructor(settings: ConfigurationSettings, operationId: string) {
            this.settings = settings;
            this.editingInProgress = false;
            this.operationId = operationId;
        }

        public settings: ConfigurationSettings
        public editingInProgress: boolean
        public operationId: string
    }

    export enum EditorStyles {
        Simple          = 0x00,
        Delete          = 0x01,
        Generate        = 0x02,
        StorageAccount  = 0x04,
        EditMode        = 0x08,
        CantChangeAccount= 0x10,
    }

    export class ConfigurationSettings {

        constructor(settings: any) {
            this.specialSettings = new SpecialSettings();
            this.specialSettings.accountName = new ConfigurationItem("AccountName", settings.AccountSettings, EditorStyles.Simple, "Account Name", false, true);
            this.specialSettings.primaryKey = new ConfigurationItem("AccountKey", settings.AccountSettings, EditorStyles.Generate, "Primary Key", false, true);
            this.specialSettings.secondaryKey = new ConfigurationItem("SecondaryAccountKey", settings.AccountSettings, EditorStyles.Generate, "Secondary Key");
            this.specialSettings.namespaceStorage = new StorageConnectionItem("StorageConnectionStringMaster", settings.AccountSettings.StorageConnectionStringMaster, EditorStyles.EditMode | EditorStyles.CantChangeAccount, "Namespace Account");
            this.specialSettings.diagnostics = new StorageConnectionItem("Microsoft.WindowsAzure.Plugins.Diagnostics.ConnectionString", settings.AccountSettings["Microsoft.WindowsAzure.Plugins.Diagnostics.ConnectionString"], EditorStyles.EditMode, "Diagnostics Account");
            this.scaleOutStorage = new ScaleSettings();
            this.scaleOutStorage.maxAccounts = settings.ScaleAccounts.MaxAccounts;
            this.scaleOutStorage.accounts = $.map(settings.ScaleAccounts.Accounts, (value) => StorageConnectionItem.createScaleOutAccount(value));
            this.miscSettings = $.map(settings.GeneralSettings, (value, key) => new ConfigurationItem(key, value, EditorStyles.Simple));
        }

        public specialSettings: SpecialSettings
        public scaleOutStorage: ScaleSettings
        public miscSettings: ConfigurationItem[]

        public toString(): string {
            var objectToSerialize = {
                AccountSettings: this.mapObject(this.specialSettings, (value: ConfigurationItem) => value.setting, (value: ConfigurationItem) => value.getValue()),
                ScaleAccounts: {
                    Accounts: this.scaleOutStorage.accounts.map((value: StorageConnectionItem) => {
                        return {
                            AccountName: value.accountName,
                            AccountKey: value.accountKey
                        }
                    })
                },
                GeneralSettings: this.mapObject(this.miscSettings,(value: ConfigurationItem) => value.setting,(value: ConfigurationItem) => value.getValue()),
            };
            return angular.toJson(objectToSerialize, true);
        }

        private mapObject(srcObject: any, keyCallback: (value: any) => string, valueCallback: (value: any) => any): any {
            var retval = {};
            for (var key in srcObject) {
                var item = srcObject[key];
                retval[keyCallback(item)] = valueCallback(item);
            }
            return retval;
        }
    }

    export class SpecialSettings {
        public accountName: ConfigurationItem
        public primaryKey: ConfigurationItem
        public secondaryKey: ConfigurationItem
        public namespaceStorage: ConfigurationItem
        public diagnostics: ConfigurationItem
    }

    export class ScaleSettings {
        public maxAccounts: number
        public accounts: StorageConnectionItem[]
    }

    export class ConfigurationItem {

        constructor(setting: string, value: string, editorStyles: EditorStyles, displayLabel?: string, isNew?: boolean, isRequired?: boolean);
        constructor(setting: string, value: any, editorStyles: EditorStyles, displayLabel?: string, isNew?: boolean, isRequired?: boolean) {
            this.setting = setting;
            this.updatedValue = jQuery.isPlainObject(value) ? value[setting] : value;
            this.value = this.updatedValue;
            this.editing = false;
            this.editorStyles = editorStyles;
            this.displayLabel = displayLabel || setting;
            this.isNew = isNew || false;
            this.isRequired = isRequired || false;
        }

        public setting: string
        public updatedValue: string
        public value: string
        public displayLabel: string
        public editing: boolean
        public editorStyles: EditorStyles
        public isNew: boolean
        public isRequired: boolean

        public toggleEdit(discardChanges: boolean): boolean {
            var retval: boolean = false;
            if ((this.editorStyles & EditorStyles.EditMode) == 0 || this.editing) {
                if (discardChanges) {
                    this.discardChanges();
                }
                else {
                    retval = this.commitChanges();
                }
            }
            this.editing = !this.editing;
            return retval;
        }

        public isEditorStyle(style: EditorStyles): boolean {
            return (this.editorStyles & style) === style;
        }

        public isEditorStyleDelete(): boolean {
            return this.isEditorStyle(EditorStyles.Delete);
        }

        public isEditorStyleGenerate(): boolean {
            return this.isEditorStyle(EditorStyles.Generate);
        }

        public isEditorStyleStorageAccount(): boolean {
            return this.isEditorStyle(EditorStyles.StorageAccount);
        }

        public isEditorStyleEditMode(): boolean {
            return this.isEditorStyle(EditorStyles.EditMode);
        }

        public isEditorStyleCantChangeAccount(): boolean {
            return this.isEditorStyle(EditorStyles.CantChangeAccount);
        }

        public commitChanges(): boolean {
            var newValue = this.getValue();
            var retval: boolean = this.updatedValue !== newValue;
            this.updatedValue = newValue;
            return retval;
        }

        public generateStorageKey() {
            var charRange = "0123456789abcdef";
            var unencoded = "";

            for (var i = 0; i < 64; i++) {
                unencoded += charRange.charAt(Math.floor(Math.random() * charRange.length));
            }
            this.value = $.base64.encode(unencoded);
        }

        public getValue(): string {
            return this.value;
        }

        protected discardChanges() {
            this.value = this.updatedValue;
        }
    }

    export class StorageConnectionItem extends ConfigurationItem {

        constructor(setting: string, value: any, editStyles: EditorStyles, displayLabel?: string, isNew?: boolean) {
            super(setting, (typeof(value) === "string" ? value : ""), editStyles | EditorStyles.StorageAccount, displayLabel, isNew);

            if (typeof (value) === "string") {
                // Parse the connection string
                var comps = this.updatedValue.split(';');
                if (comps.length > 0) {
                    comps.forEach((keyValue, index) => {
                        // Can't use .split() here as base64-encoded values may include '=' sign
                        var charIndex = keyValue.indexOf('=');
                        var key = keyValue.substr(0, charIndex).toLowerCase();
                        var value = keyValue.substr(charIndex + 1);
                        if (key === 'accountname') {
                            this.accountName = value;
                        }
                        else if (key === 'accountkey') {
                            this.accountKey = value;
                        }
                    });
                }
            }
            else {
                this.accountName = value.AccountName;
                this.accountKey = value.AccountKey;
            }
            this.updatedAccountName = this.originalAccountName = this.accountName;
            this.updatedAccountKey = this.accountKey;
        }

        public static createScaleOutAccount(connectionString: string, isNew?: boolean): StorageConnectionItem {
            var retval = new StorageConnectionItem("", connectionString, EditorStyles.EditMode | EditorStyles.CantChangeAccount);
            if (isNew) {
                retval.isNew = true;
            }
            return retval;
        }

        public accountName: string;
        public accountKey: string;
        public updatedAccountName: string;
        public updatedAccountKey: string;
        public originalAccountName: string;
        protected discardChanges() {
            super.discardChanges();
            this.accountName = this.updatedAccountName;
            this.accountKey = this.updatedAccountKey;
        }

        public commitChanges(): boolean {
            var retval: boolean = super.commitChanges() || this.updatedAccountName !== this.accountName || this.updatedAccountKey !== this.accountKey;
            this.updatedAccountName = this.accountName;
            this.updatedAccountKey = this.accountKey;
            return retval;
        }

        public getValue(): string {
            this.value = "DefaultEndpointsProtocol=https;AccountName=" + this.accountName + ";AccountKey=" + (this.accountKey || "");
            return this.value;
        }
    }
} 