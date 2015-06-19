'use strict';
angular.module('dashApp')
.factory('configurationSvc', ['$http', function ($http) {
    var apiHost = '/api/configuration/';
    return {
        getItems : function(){
            return $http.get(apiHost);
        },
        getItem : function(name){
            return $http.get(apiHost + '/?servicename=' + name);
        }
    };
}]);