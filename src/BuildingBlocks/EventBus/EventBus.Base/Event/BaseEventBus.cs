using EventBus.Base.Abstraction;
using EventBus.Base.SubManagers;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace EventBus.Base.Event
{
    public abstract class BaseEventBus : IEventBus
    {
        public readonly IServiceProvider ServiceProvider;
        public readonly IEventBusSubscriptionManager SubsManager;

        public EventBusConfig EventBusConfig { get; set; } //Dışarıdan gelecek config özellikleri

        public BaseEventBus(EventBusConfig config, IServiceProvider serviceProvider)
        {
            EventBusConfig = config;
            ServiceProvider = serviceProvider;
            SubsManager = new InMemoryEventBusSubscriptionManager(ProcessEventName); //Subscription inmemory olarak set edilmiş. Başka bir tür geldiğinde bu değişecektir.
        }

        public virtual string ProcessEventName(string eventName)
        {
            //event name verirken başından veya sonundan trim etmek için kullanılrı
            if (EventBusConfig.DeleteEventPrefix)
                eventName = eventName.TrimStart(EventBusConfig.EventNamePrefix.ToArray());

            if (EventBusConfig.DeleteEventSuffix)
                //eventName = eventName.TrimStart(EventBusConfig.EventNameSuffix.ToArray());
                eventName = eventName.Replace(EventBusConfig.EventNameSuffix, "");

                return eventName;
        }

        public virtual string GetSubName(string eventName)
        {
            //Örn : NotificationService.OrderCreatedIntegrationEvent şeklinde bir subscription name döner
            //Örn : NotificationService.OrderCreated da olabilir suffix den kırpılacaksa. Bu bizim queue name olacak
            return $"{EventBusConfig.SubscriberClientAppName}.{ProcessEventName(eventName)}";
        }

        public virtual void Dispose()
        {
            EventBusConfig = null;
            SubsManager.Clear();
        }

        public async Task<bool> ProcessEvent(string eventName, string message)
        {
            //rabbitmq veya asb üzerinden gelen mesajlar bu method a düşecek
            //event ismi ve message da deserialize edilebilir bir obje olacak
            eventName = ProcessEventName(eventName);

            var processed = false;

            if (SubsManager.HasSubscriptionsForEvent(eventName)) // Eğer gelen eventName e göre onu dinleyen biri varsa işlem yapılacaktır
            {
                var subscriptions = SubsManager.GetHandlersForEvent(eventName); //Gelen event e kimler subscribe onları bulur
                using (var scope = ServiceProvider.CreateScope())//Burdaki service providerlar aynı scope da türetilmesi için create scope yaptık
                {
                    foreach (var subscription in subscriptions)
                    {
                        var handler = ServiceProvider.GetService(subscription.HandleType); //Örn : OrderIntegrationEventHandler ı bana ver demektir. 
                        if (handler == null) continue;

                        var eventType = SubsManager.GetEventTypeByName($"{EventBusConfig.EventNamePrefix}{eventName}{EventBusConfig.EventNameSuffix}");
                        var integrationEvent = JsonConvert.DeserializeObject(message, eventType);
                        var concreteType = typeof(IIntegrationEventHandler<>).MakeGenericType(eventType);
                        await (Task)concreteType.GetMethod("Handle").Invoke(handler, new object[] { integrationEvent }); //Gelen eventin türüne göre dinamik olarak handle methodu çalışır
                        //message içerisinde integration event i yakalar ve ilgili serviste handle eder
                    }
                }
                processed = true;
            }
            return processed;
        }

        //Aşağıdaki methodların impelentasyonları rabbitmq veya asb tarafında olacağında içlerini doldurmadık
        public abstract void Publish(IntegrationEvent @event);

        public abstract void Subscribe<T, TH>() where T : IntegrationEvent where TH : IIntegrationEventHandler<T>;

        public abstract void UnSubscribe<T, TH>() where T : IntegrationEvent where TH : IIntegrationEventHandler<T>;
    }
}
