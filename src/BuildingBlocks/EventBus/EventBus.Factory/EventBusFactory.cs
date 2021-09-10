using EventBus.AzureServiceBus;
using EventBus.Base;
using EventBus.Base.Abstraction;
using EventBus.RabbitMQ;
using System;

namespace EventBus.Factory
{
    public class EventBusFactory
    {
        //Bu projenin ve sınıfın amacı, client tarafı doğrudan asb veya rabbit mq projelerini referans eklemek yerine
        //bir tek factory projesini referans ekleyerek dışarıdan parametrik olarak mesajların rabbitmq veya asb üzerinden publish edilmesini sağlayabilriz
        public static IEventBus Create(EventBusConfig config, IServiceProvider serviceProvider)
        {
            return config.EventBusType switch
            {
                EventBusType.AzureServiceBus => new EventBusServiceBus(config, serviceProvider),
                _ => new EventBusRabbitMQ(config, serviceProvider)
            };
        }
    }
}
