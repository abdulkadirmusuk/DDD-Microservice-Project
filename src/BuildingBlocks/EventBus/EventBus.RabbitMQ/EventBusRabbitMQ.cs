using EventBus.Base;
using EventBus.Base.Event;
using Newtonsoft.Json;
using Polly;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RabbitMQ.Client.Exceptions;
using System;
using System.Net.Sockets;
using System.Text;

namespace EventBus.RabbitMQ
{
    public class EventBusRabbitMQ : BaseEventBus
    {
        RabbitMQPersistentConnection persistentConnection;
        private readonly IConnectionFactory connectionFactory;
        private readonly IModel consumerChannel;
        public EventBusRabbitMQ(EventBusConfig config, IServiceProvider serviceProvider) : base(config, serviceProvider)
        {
            //RabbitMqPersistentConnection içine gidecek olan connectionFactory objesi EventBusConfig içinde oluşan Connection objesi olacaktır.
            if (config.Connection != null)
            {
                var connJson = JsonConvert.SerializeObject(EventBusConfig.Connection, new JsonSerializerSettings()
                {
                    //Self referencing loop detected for property
                    ReferenceLoopHandling = ReferenceLoopHandling.Ignore //Aynı referansta obje olması için igoner setting yaptık
                });
                connectionFactory = JsonConvert.DeserializeObject<ConnectionFactory>(connJson);
            }
            else connectionFactory = new ConnectionFactory();
            persistentConnection = new RabbitMQPersistentConnection(connectionFactory,config.ConnectionRetryCount);
            consumerChannel = CreateConsumerChannel();
            SubsManager.OnEventRemoved += SubsManager_OnEventRemoved; //Event Handler içine yeni bir event ekledik. Kaldırıldığında dinleme event i
        }

        private void SubsManager_OnEventRemoved(object sender, string eventName)
        {
            eventName = ProcessEventName(eventName);
            if (!persistentConnection.IsConnected)
            {
                persistentConnection.TryConnect();
            }

            //using var channel = persistentConnection.CreateModel();
            //Bu sefer queue yu unbind işlemi yapıyoruz. yani artık o queue yu dinlemeyi bırakmış oluyoruz.queue silme işlemi yapmadık
            consumerChannel.QueueUnbind(
                queue: ProcessEventName(eventName),
                exchange: EventBusConfig.DefaultTopicName,
                routingKey: eventName
                );
            if (SubsManager.IsEmpty)
            {
                consumerChannel.Close();
            }
        }

        public override void Publish(IntegrationEvent @event)
        {
            if (!persistentConnection.IsConnected)
            {
                persistentConnection.TryConnect();
            }

            //hata olduğunda retry mekanizması geliştirdik
            var policy = Policy.Handle<BrokerUnreachableException>()
                .Or<SocketException>()
                .WaitAndRetry(EventBusConfig.ConnectionRetryCount, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)), (ex, time) =>
                {
                    //logging
                });

            var eventName = @event.GetType().Name;
            eventName = ProcessEventName(eventName);

            //exchange olmama ihtimaline karşın exchange declare ediyorz
            consumerChannel.ExchangeDeclare(exchange: EventBusConfig.DefaultTopicName, type: "direct");//Ensure exchange exists while publishing

            var message = JsonConvert.SerializeObject(@event);
            var body = Encoding.UTF8.GetBytes(message); //message convert to byte[]

            policy.Execute(() =>
            {
                var properties = consumerChannel.CreateBasicProperties();
                properties.DeliveryMode = 2;//persistent

                //queue create edilmediyse tekrar queue create edilecek
                consumerChannel.QueueDeclare(
                        queue:GetSubName(eventName), //ensure queue exist while publishing
                        durable:true,
                        exclusive:false,
                        autoDelete:false,
                        arguments:null
                    );

                //en son hiç bir sorun yok ise basic publish ile data publish edilir.
                consumerChannel.BasicPublish(
                        exchange:EventBusConfig.DefaultTopicName,
                        routingKey:eventName,
                        mandatory:true, 
                        basicProperties:properties,
                        body:body
                    );
            });
        }

        public override void Subscribe<T, TH>()
        {
            //T:IntegrationEvent, TH:IIntegrationEventHandler
            //SubscriptionManager yardımı ile bir event e subscribe olunur.
            var eventName = typeof(T).Name;
            eventName = ProcessEventName(eventName);

            if (!SubsManager.HasSubscriptionsForEvent(eventName)) //event daha önce subscibe edilmiyor ise..
            {
                if (!persistentConnection.IsConnected)
                {
                    persistentConnection.TryConnect();
                }
                //queue declaer işlemi(oluşurma)
                consumerChannel.QueueDeclare(
                    queue: GetSubName(eventName), //Örn:NotificationService.OrderCreated şeklinde bir queue name olacak
                    durable: true,
                    exclusive: false,
                    autoDelete: false,
                    arguments: null);
                //queue bind işlemi(queue exchange ve bind işlemi)
                consumerChannel.QueueBind(
                    queue: GetSubName(eventName),
                    exchange: EventBusConfig.DefaultTopicName,
                    routingKey: eventName);
            }
            SubsManager.AddSubscription<T, TH>();//ilgili queue ya subscribe olduk. InMemoryManager da bundan haberdar edildi
            StartBasicConsume(eventName);//en son consumer eklenir. Dinleme işlemi başlamış olur
            
        }

        public override void UnSubscribe<T, TH>()
        {
            SubsManager.RemoveSubscription<T, TH>();
            //sadece sub manager dan remove etmek yetiyor. rabbit mq tarafından neden remove etmedik derseniz.
            //Remove edildiğinde bir event eklemiştik o event e ctor içinde SubsManager_OnEventRemoved diye bir işlem dinlemesini söyledik
        }

        private IModel CreateConsumerChannel()
        {
            if (!persistentConnection.IsConnected)
            {
                persistentConnection.TryConnect();
            }
            var channel = persistentConnection.CreateModel();
            channel.ExchangeDeclare(exchange: EventBusConfig.DefaultTopicName, type: "direct");
            return channel;
        }

        private void StartBasicConsume(string eventName)
        {
            if (consumerChannel !=null)
            {
                var consumer = new EventingBasicConsumer(consumerChannel); //consumer yarattık
                consumer.Received += Consumer_Received;
                
                //consumer tanımı yapıldı
                consumerChannel.BasicConsume(
                    queue: GetSubName(eventName),
                    autoAck: false, //oto ack işlemi yapmasın
                    consumer: consumer
                    );
            }
        }

        private async void Consumer_Received(object sender, BasicDeliverEventArgs eventArgs)
        {
            var eventName = eventArgs.RoutingKey;
            eventName = ProcessEventName(eventName);
            var message = Encoding.UTF8.GetString(eventArgs.Body.Span);//Gelen mesajı string e çevirdik

            try
            {
                await ProcessEvent(eventName, message); //Process etmesi için ProcessEvent methoduna gönderdik.
            }
            catch (Exception ex) when (ex!=null)
            {
                //logging
                //ignore
            }
            consumerChannel.BasicAck(eventArgs.DeliveryTag, multiple: false);
        }
    }
}
