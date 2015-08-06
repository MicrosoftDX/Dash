'use strict';
angular.module('dashApp')
.controller('configurationCtrl', ['$scope', '$location', 'configurationSvc', 'adalAuthenticationService', function ($scope, $location, configurationSvc, adalService) {
    $scope.error = "";
    $scope.loadingMessage = "Loading...";
    $scope.settings = null;
    $scope.editingInProgress = false;
    $scope.newTodoCaption = "";


    $scope.editInProgressTodo = {
        Description: "",
        ID: 0
    };

    

    $scope.editSwitch = function (todo) {
        todo.edit = !todo.edit;
        if (todo.edit) {
            $scope.editInProgressTodo.Description = todo.Description;
            $scope.editInProgressTodo.ID = todo.ID;
            $scope.editingInProgress = true;
        } else {
            $scope.editingInProgress = false;
        }
    };

    $scope.populate = function () {
        configurationSvc.getItems().success(function (results) {
            console.debug('Results ' + results);
            $scope.settings = results;
            $scope.loadingMessage = "";
        }).error(function (err) {
            $scope.error = err;
            $scope.loadingMessage = "";
        })
    };
    $scope.update = function (todo) {
        todoListSvc.putItem($scope.editInProgressTodo).success(function (results) {
            $scope.loadingMsg = "";
            $scope.populate();
            $scope.editSwitch(todo);
        }).error(function (err) {
            $scope.error = err;
            $scope.loadingMessage = "";
        })
    };
}]);