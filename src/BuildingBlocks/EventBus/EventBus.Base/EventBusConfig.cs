using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EventBus.Base
{
    //EventBus RabbitMQ ya mı ASB yi bağlanacak
    //Retry mekanizması kaç kere bağlanmayı deneyecek gibi ayarlar burada yapılır
    public class EventBusConfig
    {
        public int ConnectionRetryCount { get; set; } = 5; //rabbit mq ya bağlanırken deneme sayısı
        public string DefaultTopicName { get; set; } = "SellingEventBus";
        public string EventBusConnectionString { get; set; } = String.Empty;
        public string SubscriberClientAppName { get; set; } = String.Empty; //Hangi servis yeni bir queu yaratacak.(Client adı servis adı olacak). Kuyrukların isimlerinin başında application name olacak. Örn: OrderService.OrderCreated.Bir event i birden fazla servis dinleyebilir.
        public string EventNamePrefix { get; set; } = String.Empty;
        public string EventNameSuffix { get; set; } = "IntegrationEvent";
        public EventBusType EventBusType { get; set; } = EventBusType.RabbitMQ; //RabbitMQ veya ASB arasında geçiş kontrolü
        public object Connection { get; set; } //Connection türü RAbbitMQ veya ASB olabilir. O yüzden object tanımlandı.ConnectionFactoryModel türü seçimi özgür bırakılmıştır.

        public bool DeleteEventPrefix => !String.IsNullOrEmpty(EventNamePrefix);
        public bool DeleteEventSuffix => !String.IsNullOrEmpty(EventNameSuffix);
    }
    public enum EventBusType
    {
        RabbitMQ = 0,
        AzureServiceBus=1
    }
}
