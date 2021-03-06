using System;
using System.Collections.Generic;
using System.Threading;
using System.Transactions;
using Microsoft.ServiceBus.Messaging;


namespace NServiceBus.Azure.Transports.WindowsAzureServiceBus
{
    using NServiceBus.Transports;
    using Unicast;
    using Unicast.Queuing;

    /// <summary>
    /// 
    /// </summary>
    public class AzureServiceBusTopicPublisher : IPublishMessages
    {
        readonly Configure config;
        public const int DefaultBackoffTimeInSeconds = 10;
        public int MaxDeliveryCount { get; set; }

        public ICreateTopicClients TopicClientCreator { get; set; }

        private readonly Dictionary<string, TopicClient> senders = new Dictionary<string, TopicClient>();
        private static readonly object SenderLock = new Object();

        public AzureServiceBusTopicPublisher(Configure config)
        {
            this.config = config;
        }

        public void Publish(TransportMessage message, PublishOptions options)
        {
            var sender = GetTopicClientForDestination(Address.Local);

            if (sender == null) throw new QueueNotFoundException { Queue = Address.Local };

            if (!config.Settings.Get<bool>("Transactions.Enabled") || Transaction.Current == null)
                Send(message, sender, options);
            else
                Transaction.Current.EnlistVolatile(new SendResourceManager(() => Send(message, sender, options)), EnlistmentOptions.None);

        }

        // todo, factor out... to bad IMessageSender is internal
        private void Send(TransportMessage message, TopicClient sender, PublishOptions options)
        {
            var numRetries = 0;
            var sent = false;

            while (!sent)
            {
                try
                {
                    SendTo(message, sender, options);
                    sent = true;
                }
                // todo, outbox
                catch (MessagingEntityDisabledException)
                {
                    numRetries++;

                    if (numRetries >= MaxDeliveryCount) throw;

                    Thread.Sleep(TimeSpan.FromSeconds(numRetries * DefaultBackoffTimeInSeconds));
                }
                // back off when we're being throttled
                catch (ServerBusyException)
                {
                    numRetries++;

                    if (numRetries >= MaxDeliveryCount) throw;

                    Thread.Sleep(TimeSpan.FromSeconds(numRetries * DefaultBackoffTimeInSeconds));
                }
                // took to long, maybe we lost connection
                catch (TimeoutException)
                {
                    numRetries++;

                    if (numRetries >= MaxDeliveryCount) throw;

                    Thread.Sleep(TimeSpan.FromSeconds(numRetries * DefaultBackoffTimeInSeconds));
                }
                // connection lost
                catch (MessagingCommunicationException)
                {
                    numRetries++;

                    if (numRetries >= MaxDeliveryCount) throw;

                    Thread.Sleep(TimeSpan.FromSeconds(numRetries * DefaultBackoffTimeInSeconds));
                }
            }
        }

        // todo, factor out... to bad IMessageSender is internal
        private void SendTo(TransportMessage message, TopicClient sender, PublishOptions options)
        {
            using (var brokeredMessage = message.Body != null ? new BrokeredMessage(message.Body) : new BrokeredMessage())
            {
                brokeredMessage.CorrelationId = message.CorrelationId;
                if (message.TimeToBeReceived < TimeSpan.MaxValue) brokeredMessage.TimeToLive = message.TimeToBeReceived;

                foreach (var header in message.Headers)
                {
                    brokeredMessage.Properties[header.Key] = header.Value;
                }

                brokeredMessage.Properties[Headers.MessageIntent] = message.MessageIntent.ToString();
                brokeredMessage.MessageId = message.Id;
                
                if (message.ReplyToAddress != null)
                {
                    brokeredMessage.ReplyTo = new DeterminesBestConnectionStringForAzureServiceBus().Determine(config.Settings, message.ReplyToAddress);
                }
                else if (options.ReplyToAddress != null)
                {
                    brokeredMessage.ReplyTo = new DeterminesBestConnectionStringForAzureServiceBus().Determine(config.Settings, options.ReplyToAddress);
                }

                if (message.TimeToBeReceived < TimeSpan.MaxValue)
                {
                    brokeredMessage.TimeToLive = message.TimeToBeReceived;
                }

                sender.Send(brokeredMessage);
                
            }
        }

        // todo, factor out...
        private TopicClient GetTopicClientForDestination(Address destination)
        {
            var key = destination.ToString();
            TopicClient sender;
            if (!senders.TryGetValue(key, out sender))
            {
                lock (SenderLock)
                {
                    if (!senders.TryGetValue(key, out sender))
                    {
                        try
                        {
                            sender = TopicClientCreator.Create(destination);
                            senders[key] = sender;
                        }
                        catch (MessagingEntityNotFoundException)
                        {
                            // TopicNotFoundException?
                            //throw new QueueNotFoundException { Queue = Address.Parse(destination) };
                        }
                        catch (MessagingEntityAlreadyExistsException)
                        {
                            // is ok.
                        }
                    }
                }
            }
            return sender;
        }
    }
}