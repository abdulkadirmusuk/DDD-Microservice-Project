using System;

namespace EventBus.Base
{
    //Dışarıdan gelen verilerin içerde tutulması için kullanılacak
    public class SubscriptionInfo
    {
        public Type HandleType { get; } //IntegrationEvent Tipini tutacak ve daha sonra bu tip üzerinden onun handle methoduna ulaşılacak

        public SubscriptionInfo(Type handleType)
        {
            HandleType = handleType ?? throw new ArgumentNullException(nameof(handleType));
        }

        public static SubscriptionInfo Typed(Type handlerType)
        {
            return new SubscriptionInfo(handlerType);
        }
    }
}
