// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using Microsoft.Data.Entity.Migrations.Design;
using Microsoft.Framework.ConfigurationModel;

namespace EntityFramework.Tool
{
    class Program
    {
        static int Main(string[] args)
        {
            string command = null;
            string[] commandArgs = null;

            if (args != null && args.Any())
            {
                command = args.First();
                commandArgs = args.Skip(1).ToArray();
            }

            try
            {
                Run(command, commandArgs);

                return 0;
            }
#if NET451
            catch (Exception exception)
            {
                Console.WriteLine(exception.Message);
#else
            catch
            {
#endif
                return 1;
            }
        }

        public static void Run(string command, params string[] commandArgs)
        {
            var tool = new MigrationTool();

            if (string.Equals(command, "config", StringComparison.OrdinalIgnoreCase))
            {
                AddCommandLine(tool.Configuration, commandArgs);
                tool.Configure();
            }
            else if (string.Equals(command, "create", StringComparison.OrdinalIgnoreCase))
            {
                AddCommandLine(tool.Configuration, commandArgs);
                tool.CreateMigration();
            }
            else if (string.Equals(command, "list", StringComparison.OrdinalIgnoreCase))
            {
                AddCommandLine(tool.Configuration, commandArgs);
                tool.ListMigrations();
            }
            else if (string.Equals(command, "script", StringComparison.OrdinalIgnoreCase))
            {
                AddCommandLine(tool.Configuration, commandArgs);
                tool.GenerateScript();
            }
            else if (string.Equals(command, "apply", StringComparison.OrdinalIgnoreCase))
            {
                AddCommandLine(tool.Configuration, commandArgs);
                tool.UpdateDatabase();
            }
            else
            {
                throw new InvalidOperationException(Strings.ToolUsage);
            }
        }

        private static void AddCommandLine(Configuration configuration, string[] commandArgs)
        {
            if (commandArgs != null && commandArgs.Any())
            {
                configuration.AddCommandLine(commandArgs);
            }
        }
    }
}
