using NServiceBus.Timeout.Core;
using NServiceBus.Timeout.Hosting.Windows;

namespace NServiceBus
{
    using System;
    using Azure;
    using Config;

    public static class ConfigureTimeoutManager
    {
        /// <summary>
        /// Use the in azure timeout persister implementation.
        /// </summary>
        /// <param name="config"></param>
        /// <returns></returns>
        public static Configure UseAzureTimeoutPersister(this Configure config)
        {
            var configSection = config.Settings.GetConfigSection<AzureTimeoutPersisterConfig>() ?? new AzureTimeoutPersisterConfig();

            //todo: refactor this
            //config.Configurer.ConfigureComponent<TimeoutPersisterReceiver>(DependencyLifecycle.SingleInstance);
            //config.Configurer.ConfigureComponent<DefaultTimeoutManager>(DependencyLifecycle.SingleInstance);

            ServiceContext.TimeoutDataTableName = configSection.TimeoutDataTableName;
            ServiceContext.TimeoutManagerDataTableName = configSection.TimeoutManagerDataTableName;

            config.Configurer.ConfigureComponent<TimeoutPersister>(DependencyLifecycle.InstancePerCall)
                .ConfigureProperty(tp => tp.ConnectionString, configSection.ConnectionString)
                .ConfigureProperty(tp => tp.CatchUpInterval, configSection.CatchUpInterval)
                .ConfigureProperty(tp => tp.PartitionKeyScope, configSection.PartitionKeyScope); 

            return config;
        }
    }
}