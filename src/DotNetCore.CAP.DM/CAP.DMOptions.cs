﻿// Copyright (c) .NET Core Community. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

// ReSharper disable once CheckNamespace
namespace DotNetCore.CAP
{
    public class DMOptions : EFOptions
    {
        /// <summary>
        /// Gets or sets the database's connection string that will be used to store database entities.
        /// </summary>
        public string ConnectionString { get; set; }
    }

    internal class ConfigureDMOptions : IConfigureOptions<DMOptions>
    {
        private readonly IServiceScopeFactory _serviceScopeFactory;

        public ConfigureDMOptions(IServiceScopeFactory serviceScopeFactory)
        {
            _serviceScopeFactory = serviceScopeFactory;
        }

        public void Configure(DMOptions options)
        {
            if (options.DbContextType != null)
            {
                using var scope = _serviceScopeFactory.CreateScope();
                var provider = scope.ServiceProvider;
                using var dbContext = (DbContext)provider.GetRequiredService(options.DbContextType);
                options.ConnectionString = dbContext.Database.GetDbConnection().ConnectionString;
            }
        }
    }
}