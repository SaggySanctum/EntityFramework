// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using JetBrains.Annotations;
using Microsoft.Data.Entity.Infrastructure;
using Microsoft.Data.Entity.Migrations.Design.Utilities;
using Microsoft.Data.Entity.Migrations.Infrastructure;
using Microsoft.Data.Entity.Relational;
using Microsoft.Framework.ConfigurationModel;
using Microsoft.Framework.DependencyInjection;

namespace Microsoft.Data.Entity.Migrations.Design
{
    public class MigrationTool
    {
        private readonly Configuration _configuration;

        public MigrationTool([NotNull] Configuration configuration)
        {
            Check.NotNull(configuration, "configuration");

            _configuration = configuration;
        }

        public MigrationTool()
            : this(new Configuration())
        {
        }

        public virtual Configuration Configuration
        {
            get { return _configuration; }
        }

        // config --ContextAssembly=x --ContextType=x --MigrationAssembly=x --MigrationNamespace=x --MigrationDirectory=x --References=x;y;z
        public virtual void Configure()
        {
            Configure(
                new[]
                    {
                        "ContextAssembly",
                        "ContextType",
                        "MigrationAssembly",
                        "MigrationNamespace",
                        "MigrationDirectory",
                        "References"
                    });
        }

        protected virtual void Configure(string[] keys)
        {
            throw new NotImplementedException();
        }

        // create --MigrationName=x --ContextAssembly=x --ContextType=x --MigrationAssembly=x --MigrationNamespace=x --MigrationDirectory=x --References=x;y;z
        public virtual ScaffoldedMigration CreateMigration()
        {
            var migrationName = Configuration.Get("MigrationName");
            if (string.IsNullOrEmpty(migrationName))
            {
                throw new InvalidOperationException(Strings.MigrationNameNotSpecified);
            }

            var migrationDirectory = Configuration.Get("MigrationDirectory");
            if (string.IsNullOrEmpty(migrationDirectory))
            {
                throw new InvalidOperationException(Strings.MigrationDirectoryNotSpecified);
            }

            using (var context = LoadContext())
            {
                ConfigureContext(context);

                var scaffolder = CreateScaffolder(context.Configuration, migrationDirectory);
                var scaffoldedMigration = scaffolder.ScaffoldMigration(migrationName);

                WriteMigration(migrationDirectory, scaffoldedMigration);

                return scaffoldedMigration;
            }
        }

        protected virtual MigrationScaffolder CreateScaffolder(
            DbContextConfiguration contextConfiguration, string migrationDirectory)
        {
            return
                new MigrationScaffolder(
                    contextConfiguration,
                    contextConfiguration.Services.ServiceProvider.GetService<MigrationAssembly>(),
                    contextConfiguration.Services.ServiceProvider.GetService<ModelDiffer>(),
                    new CSharpMigrationCodeGenerator(new CSharpModelCodeGenerator()))
                    {
                        MigrationDirectory = migrationDirectory
                    };
        }

        protected virtual void WriteMigration(string migrationDirectory, ScaffoldedMigration scaffoldedMigration)
        {
            scaffoldedMigration.MigrationFile = Path.Combine(migrationDirectory, scaffoldedMigration.MigrationClass + ".cs");
            scaffoldedMigration.MigrationMetadataFile = Path.Combine(migrationDirectory, scaffoldedMigration.MigrationClass + ".Designer.cs");
            scaffoldedMigration.SnapshotModelFile = Path.Combine(migrationDirectory, scaffoldedMigration.SnapshotModelClass + ".cs");

            WriteFile(scaffoldedMigration.MigrationFile, scaffoldedMigration.MigrationCode, overwrite: false);
            WriteFile(scaffoldedMigration.MigrationMetadataFile, scaffoldedMigration.MigrationMetadataCode, overwrite: false);
            WriteFile(scaffoldedMigration.SnapshotModelFile, scaffoldedMigration.SnapshotModelCode, overwrite: true);
        }

        protected virtual void WriteFile(string path, string content, bool overwrite)
        {
#if NET451
            var fileMode = overwrite ? FileMode.Create : FileMode.CreateNew;

            using (var stream = new FileStream(path, fileMode, FileAccess.Write))
            {
                using (var writer = new StreamWriter(stream, Encoding.UTF8))
                {
                    writer.Write(content);
                }
            }
#else
            throw new NotImplementedException();
#endif
        }

        // list --ContextAssembly=x --ContextType=x --References=x;y;z
        public virtual IReadOnlyList<IMigrationMetadata> ListMigrations()
        {
            using (var context = LoadContext())
            {
                ConfigureContext(context);

                var migrator = context.Configuration.Services.ServiceProvider.GetService<Migrator>();

                return migrator.GetDatabaseMigrations();
            }
        }

        // script --TargetMigration=x --ContextAssembly=x --ContextType=x --MigrationAssembly=x --MigrationNamespace=x --References=x;y;z
        public virtual IReadOnlyList<SqlStatement> GenerateScript()
        {
            var targetMigrationName = Configuration.Get("TargetMigration");

            using (var context = LoadContext())
            {
                ConfigureContext(context);

                var migrator = context.Configuration.Services.ServiceProvider.GetService<Migrator>();

                return
                    string.IsNullOrEmpty(targetMigrationName)
                        ? migrator.GenerateUpdateDatabaseSql()
                        : migrator.GenerateUpdateDatabaseSql(targetMigrationName);
            }
        }

        // apply --TargetMigration=x --ContextAssembly=x --ContextType=x --MigrationAssembly=x --MigrationNamespace=x --References=x;y;z
        public virtual void UpdateDatabase()
        {
            var targetMigrationName = Configuration.Get("TargetMigration");

            using (var context = LoadContext())
            {
                ConfigureContext(context);

                var migrator = context.Configuration.Services.ServiceProvider.GetService<Migrator>();

                if (string.IsNullOrEmpty(targetMigrationName))
                {
                    migrator.UpdateDatabase();
                }
                else
                {
                    migrator.UpdateDatabase(targetMigrationName);
                }
            }            
        }

        protected virtual DbContext LoadContext()
        {
            var contextAssemblyFile = Configuration.Get("ContextAssembly");
            if (string.IsNullOrEmpty(contextAssemblyFile))
            {
                throw new InvalidOperationException(Strings.ContextAssemblyNotSpecified);
            }

            // TODO: Figure out what we want to do about loading the context assembly and its references:
            // 1. Provide options for specifying the references and load them using Assembly.LoadFrom or,
            // 2. Provide options for specifying the locations of the references and load them using a ResolveEventHandler or,
            // 3. Create a new AppDomain and provide options to specify the ApplicationBase, DynamicBase, PrivateBinPath or,
            // 4. Something else.

            var contextAssembly = LoadAssembly(contextAssemblyFile);

            LoadReferences();

            var contextTypeName = Configuration.Get("ContextType");
            var contextType
                = string.IsNullOrEmpty(contextTypeName)
                    ? FindContextType(contextAssembly)
                    : GetContextType(contextTypeName, contextAssembly);            

            return CreateContext(contextType);
        }

        protected virtual Type GetContextType(string contextTypeName, Assembly contextAssembly)
        {
            var contextType = contextAssembly.GetType(contextTypeName);
            if (contextType == null)
            {
                throw new InvalidOperationException(Strings.FormatAssemblyDoesNotContainType(contextAssembly.FullName, contextTypeName));
            }

            if (!typeof(DbContext).GetTypeInfo().IsAssignableFrom(contextType.GetTypeInfo()))
            {
                throw new InvalidOperationException(Strings.FormatTypeIsNotDbContext(contextTypeName));
            }

            return contextType;
        }

        protected virtual Type FindContextType(Assembly contextAssembly)
        {
            var contextTypes 
                = contextAssembly.GetAccessibleTypes()
                    .Where(t => !typeof(DbContext).GetTypeInfo().IsAssignableFrom(t.GetTypeInfo()))
                    .ToArray();

            if (contextTypes.Length == 0)
            {
                throw new InvalidOperationException(Strings.FormatAssemblyDoesNotContainDbContext(contextAssembly.FullName));
            }

            if (contextTypes.Length > 1)
            {
                throw new InvalidOperationException(Strings.FormatAssemblyContainsMultipleDbContext(contextAssembly.FullName));
            }

            return contextTypes[0];
        }

        protected virtual void LoadReferences()
        {
            var references = Configuration.Get("References");
            if (string.IsNullOrEmpty(references))
            {
                return;
            }

            foreach (var assemblyFile in references.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
            {
                LoadAssembly(assemblyFile);
            }
        }

        protected virtual Assembly LoadAssembly(string assemblyFile)
        {
#if NET451
            return Assembly.LoadFrom(assemblyFile);
#else
            throw new NotImplementedException();
#endif
        }

        protected virtual DbContext CreateContext(Type contextType)
        {
            return (DbContext)Activator.CreateInstance(contextType);
        }

        protected virtual void ConfigureContext(DbContext context)
        {
            var extension = RelationalOptionsExtension.Extract(context.Configuration);

            var migrationAssemblyFile = Configuration.Get("MigrationAssembly");
            if (!string.IsNullOrEmpty(migrationAssemblyFile))
            {
                extension.MigrationAssembly = LoadAssembly(migrationAssemblyFile);
            }

            var migrationNamespace = Configuration.Get("MigrationNamespace");
            if (!string.IsNullOrEmpty(migrationNamespace))
            {
                extension.MigrationNamespace = migrationNamespace;
            }
        }
    }
}
