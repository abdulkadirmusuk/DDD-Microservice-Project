using EventBus.Base.Abstraction;
using EventBus.Base.Event;
using System;
using System.Collections.Generic;
using System.Linq;

namespace EventBus.Base.SubManagers
{
    public class InMemoryEventBusSubscriptionManager : IEventBusSubscriptionManager
    {
        private readonly Dictionary<string, List<SubscriptionInfo>> _handlers; //Subscription listesi tutulur
        private readonly List<Type> _eventTypes;

        public event EventHandler<string> OnEventRemoved;
        public Func<string, string> eventNameGetter;

        public InMemoryEventBusSubscriptionManager(Func<string, string> eventNameGetter)
        {
            _handlers = new Dictionary<string, List<SubscriptionInfo>>();
            _eventTypes = new List<Type>();
            this.eventNameGetter = eventNameGetter; //Bütün event isimlerinin sonuna IntegrationEvent eklememk için bu eventNameGetter ı entegre ediyoruz. Event isimleri üzerinde trim operasyonu yapar
        }

        public bool IsEmpty => !_handlers.Keys.Any(); //key var mı? kontrolü
        public void Clear() => _handlers.Clear(); //event clear


        public void AddSubscription<T, TH>()
            where T : IntegrationEvent
            where TH : IIntegrationEventHandler<T>
        {
            var eventName = GetEventKey<T>();
            AddSubscription(typeof(TH), eventName);

            if (!_eventTypes.Contains(typeof(T)))
            {
                _eventTypes.Add(typeof(T));
            }
        }

        private void AddSubscription(Type handlerType, string eventName)
        {
            //Hangi tür IntegrationEventHandler ve IntegrationEvent oluşturacağını alır ve Subscription işlemini yapar
            if (!HasSubscriptionsForEvent(eventName))// event in daha önce subscibe edilip edilmediği bilgisi
            {
                _handlers.Add(eventName, new List<SubscriptionInfo>());//yeni bir event isminde subscriptioninfo yu dictiojnary e kayıt ediyorz
            }

            if (_handlers[eventName].Any(s=>s.HandleType == handlerType))//daha önce aynı tipten bir handler event listesinde var ise hata fırlatır
            {
                throw new ArgumentException($"Handler Type {handlerType.Name} already registered for '{eventName}'", nameof(handlerType));
            }

            _handlers[eventName].Add(SubscriptionInfo.Typed(handlerType));
        }

        public string GetEventKey<T>()
        {
            //Gelen event adı (örn: eventName = OrderCreatedIntegrationEvent olur)
            string eventName = typeof(T).Name;
            return eventNameGetter(eventName); //eventName kırpılır(trim)
        }

        public Type GetEventTypeByName(string eventName) => _eventTypes.SingleOrDefault(t => t.Name == eventName);

        public IEnumerable<SubscriptionInfo> GetHandlersForEvent<T>() where T : IntegrationEvent
        {
            //handler listesini geriye döner
            var key = GetEventKey<T>();
            return GetHandlersForEvent(key);
        }

        public IEnumerable<SubscriptionInfo> GetHandlersForEvent(string eventName) => _handlers[eventName];

        private SubscriptionInfo FindSubscriptionToRemove(string eventName,Type handlerType)
        {
            if (!HasSubscriptionsForEvent(eventName))
            {
                return null;
            }

            return _handlers[eventName].SingleOrDefault(s => s.HandleType == handlerType);
        }

        public bool HasSubscriptionsForEvent<T>() where T : IntegrationEvent
        {
            var key = GetEventKey<T>();
            return HasSubscriptionsForEvent(key);
        }

        public bool HasSubscriptionsForEvent(string eventName) => _handlers.ContainsKey(eventName); //dictionary de bu isimde bir event var mı?

        public void RemoveSubscription<T, TH>()
            where T : IntegrationEvent
            where TH : IIntegrationEventHandler<T>
        {
            var handlerToRemove = FindSubscriptionToRemove<T, TH>(); //subs key i bul
            var eventName = GetEventKey<T>(); //event name i bul
            RemoveHandler(eventName, handlerToRemove); // subs key ve event adına göre kaldır
        }

        private SubscriptionInfo FindSubscriptionToRemove<T, TH>() where T : IntegrationEvent where TH : IIntegrationEventHandler<T>
        {
            var eventName = GetEventKey<T>();
            return FindSubscriptionToRemove(eventName, typeof(TH));
        }

        private void RemoveHandler(string eventName, SubscriptionInfo subsToRemove)
        {
            if (subsToRemove !=null)
            {
                _handlers[eventName].Remove(subsToRemove);
                if (!_handlers[eventName].Any())
                {
                    _handlers.Remove(eventName);
                    var eventType = _eventTypes.SingleOrDefault(e => e.Name == eventName);
                    if (eventType!=null)
                    {
                        _eventTypes.Remove(eventType);
                    }
                    RaiseOnEventRemoved(eventName);
                }
            }
        }

        private void RaiseOnEventRemoved(string eventName)
        {
            //event silindiği zaman o event i kullananlara haber verilecek
            var handler = OnEventRemoved;
            handler?.Invoke(this, eventName);
        }
    }
}
