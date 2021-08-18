using EventBus.Base.Event;
using System;
using System.Collections.Generic;

namespace EventBus.Base.Abstraction
{
    //IEventBus dan türemiş işlemler aslında burayı kullanacak. Dinamik olması için interface e bağladık.
    public interface IEventBusSubscriptionManager
    {
        bool IsEmpty { get; } //SubscriptionManager herhangi bir event dinliyor mu
        event EventHandler<string> OnEventRemoved; //unsubscribe çalıştığında burası da tetiklencek
        void AddSubscription<T, TH>() where T : IntegrationEvent where TH : IIntegrationEventHandler<T>;//Subscription ekeleme
        void RemoveSubscription<T, TH>() where TH : IIntegrationEventHandler<T> where T : IntegrationEvent; //Subscription silme
        bool HasSubscriptionsForEvent<T>() where T : IntegrationEvent;//Dışarıdan bir event gönderildiğinde zaten o event dinleniyor mu dinlenmiyor mu bakacak
        bool HasSubscriptionsForEvent(string eventName);//Dışarıdan bir event gönderildiğinde zaten o event dinleniyor mu dinlenmiyor mu bakacak
        Type GetEventTypeByName(string eventName);//event ismi gönderildiğinde onun Type ını dönecek (örn:OrderCreated gelecek geriye ise OrderCreatedIntegrationHandler tipi dönecek.Yani event in kendisine ulaşılacak)
        void Clear();//Bütün Subscription lar clear edilecek
        IEnumerable<SubscriptionInfo> GetHandlersForEvent<T>() where T : IntegrationEvent; //Gelen bir event in bütün Subscriptionları/handler larını geriye döner
        IEnumerable<SubscriptionInfo> GetHandlersForEvent(string eventName); //Gelen bir event in bütün Subscriptionları/handler larını geriye döner
        string GetEventKey<T>(); //RoutingKey içinde event isimlerinin RoutingKey ismini geriye döner
    }
}
