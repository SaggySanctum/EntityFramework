// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Threading;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Microsoft.Data.Entity.AzureTableStorage.Utilities;
using Microsoft.WindowsAzure.Storage.Table;

namespace Microsoft.Data.Entity.AzureTableStorage.Requests
{
    public abstract class TableOperationRequest : TableRequest<TableResult>
    {
        private readonly TableOperation _operation;

        protected TableOperationRequest([NotNull] AtsTable table, [NotNull] TableOperation operation)
            : base(table)
        {
            Check.NotNull(operation, "operation");
            _operation = operation;
        }

        [NotNull]
        public virtual TableOperation Operation
        {
            get { return _operation; }
        }

        protected override TableResult ExecuteOnTable([NotNull] CloudTable table, [NotNull] RequestContext requestContext)
        {
            Check.NotNull(table, "table");
            Check.NotNull(requestContext, "requestContext");
            return table.Execute(_operation, null, requestContext.OperationContext);
        }

        protected override Task<TableResult> ExecuteOnTableAsync([NotNull] CloudTable table, [NotNull] RequestContext requestContext, CancellationToken cancellationToken = default(CancellationToken))
        {
            Check.NotNull(table, "table");
            Check.NotNull(requestContext, "requestContext");
            return Task.Run(
                () => table.ExecuteAsync(_operation, null, requestContext.OperationContext, cancellationToken)
                , cancellationToken);
        }
    }
}
