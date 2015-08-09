//     Copyright (c) Microsoft Corporation.  All rights reserved.

/// <reference path="../../scripts/_references.ts" />

module Dash.Management.Model {
    "use strict";

    export class ButtonBarButton {
        constructor(public displayText: string, public enabled: boolean, private imageUrl?: string) { }
    }
} 