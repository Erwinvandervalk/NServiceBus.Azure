namespace NServiceBus.Azure.Transports.WindowsAzureServiceBus
{
    using System;
    using System.Collections.Generic;
    using Microsoft.ServiceBus.Messaging;

    public class CreatesMessagingFactories : ICreateMessagingFactories
    {
        readonly Configure config;

        private static readonly Dictionary<string, MessagingFactory> MessagingFactories = new Dictionary<string, MessagingFactory>();

        private static readonly object FactoryLock = new Object();

        public CreatesMessagingFactories(Configure config)
        {
            this.config = config;
        }

        public MessagingFactory Create(string potentialConnectionString)
        {
            var validation = new DeterminesBestConnectionStringForAzureServiceBus();
            var connectionstring = validation.IsPotentialServiceBusConnectionString(potentialConnectionString)
                                     ? potentialConnectionString
                                     : validation.Determine(config.Settings); 

            MessagingFactory factory;
            if (!MessagingFactories.TryGetValue(connectionstring, out factory))
            {
                lock (FactoryLock)
                {
                    if (!MessagingFactories.TryGetValue(connectionstring, out factory))
                    {
                        factory = MessagingFactory.CreateFromConnectionString(connectionstring);
                        MessagingFactories[connectionstring] = factory;
                    }
                }
            }
            return factory;
        }
    }
}