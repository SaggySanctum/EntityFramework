﻿// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Threading;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Microsoft.Data.Entity.AzureTableStorage.Utilities;
using Microsoft.WindowsAzure.Storage.Table;

namespace Microsoft.Data.Entity.AzureTableStorage.Requests
{
    public class CreateTableRequest : TableRequest<bool>
    {
        public CreateTableRequest([NotNull] AtsTable table)
            : base(table)
        {
        }

        public override string Name
        {
            get { return "CreateTableRequest"; }
        }

        protected override bool ExecuteOnTable([NotNull] CloudTable table, [NotNull] RequestContext requestContext)
        {
            Check.NotNull(table, "table");
            Check.NotNull(requestContext, "requestContext");
            return table.CreateIfNotExists(null, requestContext.OperationContext);
        }

        protected override Task<bool> ExecuteOnTableAsync([NotNull] CloudTable table, [NotNull] RequestContext requestContext, CancellationToken cancellationToken)
        {
            Check.NotNull(table, "table");
            Check.NotNull(requestContext, "requestContext");
            return table.CreateIfNotExistsAsync(null, requestContext.OperationContext, cancellationToken);
        }
    }
}
