// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using JetBrains.Annotations;
using Microsoft.Data.Entity.Metadata;
using Microsoft.Data.Entity.Relational.Model;
using Microsoft.Data.Entity.Relational.Utilities;
using Microsoft.Data.Entity.Utilities;
using ForeignKey = Microsoft.Data.Entity.Relational.Model.ForeignKey;
using Index = Microsoft.Data.Entity.Relational.Model.Index;

namespace Microsoft.Data.Entity.Relational
{
    public class DatabaseBuilder
    {
        // TODO: IModel may not be an appropriate cache key if we want to be
        // able to unload IModel instances and create new ones.
        private readonly ThreadSafeDictionaryCache<IModel, ModelDatabaseMapping> _mappingCache
            = new ThreadSafeDictionaryCache<IModel, ModelDatabaseMapping>();

        public virtual DatabaseModel GetDatabase([NotNull] IModel model)
        {
            Check.NotNull(model, "model");

            return GetMapping(model).Database;
        }

        public virtual ModelDatabaseMapping GetMapping([NotNull] IModel model)
        {
            Check.NotNull(model, "model");

            return _mappingCache.GetOrAdd(model, m =>
                {
                    // TODO: Consider making this lazy since we don't want to load the whole model just to
                    // save changes to a single entity.
                    var database = new DatabaseModel();
                    var mapping = new ModelDatabaseMapping(m, database);

                    foreach (var entityType in m.EntityTypes)
                    {
                        var table = BuildTable(database, entityType);
                        mapping.Map(entityType, table);

                        foreach (var property in entityType.Properties)
                        {
                            mapping.Map(property, BuildColumn(table, property));
                        }

                        var primaryKey = entityType.GetKey();
                        mapping.Map(primaryKey, BuildPrimaryKey(database, primaryKey));

                        foreach (var index in entityType.Indexes)
                        {
                            mapping.Map(index, BuildIndex(database, index));
                        }
                    }

                    foreach (var entityType in m.EntityTypes)
                    {
                        foreach (var foreignKey in entityType.ForeignKeys)
                        {
                            mapping.Map(foreignKey, BuildForeignKey(database, foreignKey));
                        }
                    }

                    return mapping;
                });
        }

        private static string PrimaryKeyName([NotNull] IKey primaryKey)
        {
            Check.NotNull(primaryKey, "primaryKey");

            return primaryKey.KeyName() ?? string.Format("PK_{0}", GetFullTableName(primaryKey.EntityType));
        }

        private static string ForeignKeyName([NotNull] IForeignKey foreignKey)
        {
            Check.NotNull(foreignKey, "foreignKey");

            return foreignKey.KeyName() ?? string.Format(
                "FK_{0}_{1}_{2}",
                GetFullTableName(foreignKey.EntityType),
                GetFullTableName(foreignKey.ReferencedEntityType),
                string.Join("_", foreignKey.Properties.OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase).Select(p => p.ColumnName())));
        }

        private static string IndexName([NotNull] IIndex index)
        {
            Check.NotNull(index, "index");

            return index.IndexName() ?? string.Format(
                "IX_{0}_{1}",
                GetFullTableName(index.EntityType),
                string.Join("_", index.Properties.OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase).Select(p => p.ColumnName())));
        }

        private static string GetFullTableName(IEntityType entityType)
        {
            var schema = entityType.Schema();
            var tableName = entityType.TableName();
            return !string.IsNullOrEmpty(schema) ? schema + "." + tableName : tableName;
        }

        private static SchemaQualifiedName GetSchemaQualifiedName(IEntityType entityType)
        {
            return new SchemaQualifiedName(entityType.TableName(), entityType.Schema());
        }

        private static Table BuildTable(DatabaseModel database, IEntityType entityType)
        {
            var table = new Table(GetSchemaQualifiedName(entityType));

            database.AddTable(table);

            return table;
        }

        private static Column BuildColumn(Table table, IProperty property)
        {
            var column =
                new Column(property.ColumnName(), property.PropertyType, property.ColumnType())
                    {
                        IsNullable = property.IsNullable,
                        DefaultValue = property.ColumnDefaultValue(),
                        DefaultSql = property.ColumnDefaultSql(),
                        ValueGenerationStrategy = property.ValueGenerationOnSave,
                        IsTimestamp = property.PropertyType == typeof(byte[]) && property.IsConcurrencyToken
                    };

            table.AddColumn(column);

            return column;
        }

        private PrimaryKey BuildPrimaryKey(DatabaseModel database, IKey primaryKey)
        {
            Check.NotNull(primaryKey, "primaryKey");

            var table = database.GetTable(GetSchemaQualifiedName(primaryKey.EntityType));
            var columns = primaryKey.Properties.Select(
                p => table.GetColumn(p.ColumnName())).ToArray();
            var isClustered = primaryKey.IsClustered();

            table.PrimaryKey = new PrimaryKey(PrimaryKeyName(primaryKey), columns, isClustered);

            return table.PrimaryKey;
        }

        private ForeignKey BuildForeignKey(DatabaseModel database, IForeignKey foreignKey)
        {
            Check.NotNull(foreignKey, "foreignKey");

            var table = database.GetTable(GetSchemaQualifiedName(foreignKey.EntityType));
            var referencedTable = database.GetTable(GetSchemaQualifiedName(foreignKey.ReferencedEntityType));
            var columns = foreignKey.Properties.Select(
                p => table.GetColumn(p.ColumnName())).ToArray();
            var referenceColumns = foreignKey.ReferencedProperties.Select(
                p => referencedTable.GetColumn(p.ColumnName())).ToArray();
            var cascadeDelete = foreignKey.CascadeDelete();

            var storeForeignKey = new ForeignKey(
                ForeignKeyName(foreignKey), columns, referenceColumns, cascadeDelete);

            table.AddForeignKey(storeForeignKey);

            return storeForeignKey;
        }

        private static Index BuildIndex(DatabaseModel database, IIndex index)
        {
            Check.NotNull(index, "index");

            var table = database.GetTable(GetSchemaQualifiedName(index.EntityType));
            var columns = index.Properties.Select(
                p => table.GetColumn(p.ColumnName())).ToArray();

            var storeIndex = new Index(
                IndexName(index), columns, index.IsUnique, index.IsClustered());

            table.AddIndex(storeIndex);

            return storeIndex;
        }
    }
}
