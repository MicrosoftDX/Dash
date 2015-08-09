//     Copyright (c) Microsoft Corporation.  All rights reserved.

/// <reference path="../../scripts/_references.ts" />

module Dash.Management.Model {
    "use strict";

    export class Configuration {
        public settings: ConfigurationSettings
        public editingInProgress: boolean
    }

    export enum EditorStyles {
        Simple          = 0x00,
        Delete          = 0x01,
        Generate        = 0x02,
        StorageAccount  = 0x04,
        EditMode        = 0x08,
    }

    export class ConfigurationSettings {

        constructor(settings: any) {
            this.specialSettings = new SpecialSettings();
            this.specialSettings.accountName = new ConfigurationItem("AccountName", settings, EditorStyles.Simple, "Account Name");
            this.specialSettings.primaryKey = new ConfigurationItem("AccountKey", settings, EditorStyles.Generate, "Primary Key");
            this.specialSettings.secondaryKey = new ConfigurationItem("SecondaryAccountKey", settings, EditorStyles.Generate, "Secondary Key");
            this.specialSettings.namespaceStorage = new ConfigurationItem("StorageConnectionStringMaster", settings, EditorStyles.StorageAccount | EditorStyles.EditMode, "Namespace Account");
            this.specialSettings.diagnostics = new ConfigurationItem("Microsoft.WindowsAzure.Plugins.Diagnostics.ConnectionString", settings, EditorStyles.StorageAccount | EditorStyles.EditMode, "Diagnostics Account");
            this.scaleOutStorage = $.map(settings.ScaleoutStorage, (value, key) => new ConfigurationItem(key, value, EditorStyles.StorageAccount | EditorStyles.Delete | EditorStyles.EditMode));
            this.miscSettings = $.map(settings.GeneralSettings, (value, key) => new ConfigurationItem(key, value, EditorStyles.Simple));
        }

        public specialSettings: SpecialSettings
        public scaleOutStorage: ConfigurationItem[]
        public miscSettings: ConfigurationItem[]
    }

    export class SpecialSettings {
        public accountName: ConfigurationItem
        public primaryKey: ConfigurationItem
        public secondaryKey: ConfigurationItem
        public namespaceStorage: ConfigurationItem
        public diagnostics: ConfigurationItem
    }

    export class ConfigurationItem {

        constructor(setting: string, value: string, editorStyles: EditorStyles, displayLabel?: string);
        constructor(setting: string, value: any, editorStyles: EditorStyles, displayLabel?: string) {
            this.setting = setting;
            this.updatedValue = jQuery.isPlainObject(value) ? value[setting] : value;
            this.value = this.updatedValue;
            this.editing = false;
            this.editorStyles = editorStyles;
            this.displayLabel = displayLabel || setting;
            this.parseConnectionValue();
        }

        public setting: string
        public updatedValue: string
        public value: string
        public displayLabel: string
        public accountName: string;
        public accountKey: string;
        public editing: boolean
        public editorStyles: EditorStyles

        public toggleEdit(discardChanges: boolean): boolean {
            var retval: boolean = false;
            if ((this.editorStyles & EditorStyles.EditMode) == 0 || this.editing) {
                if (discardChanges) {
                    this.value = this.updatedValue;
                    this.parseConnectionValue();
                }
                else {
                    retval = this.commitChanges();
                }
            }
            this.editing = !this.editing;
            return retval;
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
            if ((this.editorStyles & EditorStyles.StorageAccount) == EditorStyles.StorageAccount) {
                this.value = "DefaultEndpointsProtocol=https;AccountName=" + this.accountName + ";AccountKey=" + this.accountKey;
            }
            return this.value;
        }

        private parseConnectionValue() {
            if ((this.editorStyles & EditorStyles.StorageAccount) == EditorStyles.StorageAccount) {
                var comps = this.updatedValue.split(';');
                if (comps.length > 0) {
                    comps.forEach((value, index) => {
                        var parts = value.split('=');
                        var label = parts[0].toLowerCase();
                        if (label === 'accountname') {
                            this.accountName = parts[1];
                        }
                        else if (label === 'accountkey') {
                            this.accountKey = parts[1];
                        }
                    });
                }
            }
        }
    }
} 