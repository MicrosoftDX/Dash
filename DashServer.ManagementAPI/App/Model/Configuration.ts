/// <reference path="../../scripts/_references.ts" />

module Dash.Management.Model {
    "use strict";

    export class Configuration {
        public error : string
        public loadingMessage : string
        public settings: ConfigurationSettings
        public editingInProgress: boolean
    }

    export class ConfigurationSettings {

        constructor(settings: any) {
            this.specialSettings = new SpecialSettings();
            this.specialSettings.accountName = new ConfigurationItem("AccountName", settings);
            this.specialSettings.primaryKey = new ConfigurationItem("AccountKey", settings);
            this.specialSettings.secondaryKey = new ConfigurationItem("SecondaryAccountKey", settings);
            this.specialSettings.namespaceStorage = new ConfigurationItem("StorageConnectionStringMaster", settings);
            this.specialSettings.diagnostics = new ConfigurationItem("Microsoft.WindowsAzure.Plugins.Diagnostics.ConnectionString", settings);
            this.scaleOutStorage = $.map(settings.ScaleoutStorage, (value, key) => new ConfigurationItem(key, value));
            this.miscSettings = $.map(settings.GeneralSettings, (value, key) => new ConfigurationItem(key, value));
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

        constructor(setting: string, value: string);
        constructor(setting: string, value: any) {
            this.setting = setting;
            this.value = jQuery.isPlainObject(value) ? value[setting] : value;
            this.editing = false;
        }

        public setting: string
        public value: string
        public editing: boolean

        public toggleEdit() {
            this.editing = !this.editing;
        }
    }
} 