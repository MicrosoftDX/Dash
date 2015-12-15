declare module adal.shared {

    /**
     * Interface for navigation routes.
     */
    export interface INavRoute {
        title?: string;
        controller?: string|Function;
        templateUrl?: string;
        controllerAs?: string;
        requireADLogin?: boolean;
        showInNav?: boolean;
    }

} 