// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Data.SqlClient;
using System.Reflection;
using Microsoft.Framework.ConfigurationModel;
using Xunit;

namespace Microsoft.Data.Entity.Migrations.Design.Tests
{
    public class MigrationToolTest
    {
        [Fact]
        public void CreateMigration()
        {
            var tool = new MyMigrationTool();
            tool.Configuration.AddCommandLine(
                new[]
                    {
                        "--MigrationName=MyMigration", 
                        "--ContextAssembly=Microsoft.Data.Entity.Migrations.Design.Tests.dll", 
                        "--ContextType=Microsoft.Data.Entity.Migrations.Design.Tests.MigrationToolTest+MyContext", 
                        "--MigrationAssembly=Microsoft.Data.Entity.Migrations.Design.Tests.dll", 
                        "--MigrationNamespace=MyNamespace", 
                        "--MigrationDirectory=MyDirectory"
                    });

            var scaffoldedMigration = tool.CreateMigration();

            Assert.Equal("MyNamespace", scaffoldedMigration.MigrationNamespace);
            Assert.Equal("MyMigration", scaffoldedMigration.MigrationClass);
            Assert.Equal("MyContextModelSnapshot", scaffoldedMigration.SnapshotModelClass);
            Assert.Equal("MyDirectory\\MyMigration.cs", scaffoldedMigration.MigrationFile);
            Assert.Equal("MyDirectory\\MyMigration.Designer.cs", scaffoldedMigration.MigrationMetadataFile);
            Assert.Equal("MyDirectory\\MyContextModelSnapshot.cs", scaffoldedMigration.SnapshotModelFile);
        }

        public class MyContext : DbContext
        {
            protected override void OnConfiguring(DbContextOptions options)
            {
                options.UseSqlServer(new SqlConnection());
            }
        }

        private class MyMigrationTool : MigrationTool
        {
            protected override Assembly LoadAssembly(string assemblyFile)
            {
                return Assembly.GetExecutingAssembly();
            }

            protected override void WriteFile(string path, string content, bool overwrite)
            {
            }
        }
    }
}
