// Copyright (c) .NET Core Community. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using DotNetCore.CAP.DM;
using DotNetCore.CAP.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

// ReSharper disable once CheckNamespace
namespace DotNetCore.CAP
{
    internal class DMCapOptionsExtension : ICapOptionsExtension
    {
        private readonly Action<DMOptions> _configure;

        public DMCapOptionsExtension(Action<DMOptions> configure)
        {
            _configure = configure;
        }

        public void AddServices(IServiceCollection services)
        {
            services.AddSingleton<CapStorageMarkerService>();
            services.AddSingleton<IDataStorage, DMDataStorage>();
            
            services.TryAddSingleton<IStorageInitializer, DMStorageInitializer>();

            //Add DMOptions
            services.Configure(_configure);
            services.AddSingleton<IConfigureOptions<DMOptions>, ConfigureDMOptions>();
        } 
    }
}