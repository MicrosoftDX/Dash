//     Copyright (c) Microsoft Corporation.  All rights reserved.

using System;

namespace DashServer.ManagementAPI.Models
{
    public class OperationResult
    {
        public string OperationId { get; set; }
        public string ErrorCode { get; set; }
        public string ErrorMessage { get; set; }
    }

    public class OperationState
    {
        public string Id { get; set; }
        public string Status { get; set; }
        public string Message { get; set; }
    }
}