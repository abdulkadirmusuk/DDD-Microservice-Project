using EventBus.Base;
using EventBus.Base.Event;
using Microsoft.Azure.ServiceBus;
using Microsoft.Azure.ServiceBus.Management;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Text;
using System.Threading.Tasks;

namespace EventBus.AzureServiceBus
{
    public class EventBusServiceBus : BaseEventBus
    {
        private ITopicClient topicClient;
        private ManagementClient managementClient;
        private ILogger logger;

        public EventBusServiceBus(EventBusConfig config, IServiceProvider serviceProvider) : base(config, serviceProvider)
        {
            //BaseEventBus tarafına config ve serviceProvider nesnesini göndeririz ve o tarafta ctor da konfigurasyonu yapılmış olur
            logger = serviceProvider.GetService(typeof(ILogger<EventBusServiceBus>)) as ILogger<EventBusServiceBus>;
            managementClient = new ManagementClient(config.EventBusConnectionString);
            topicClient = createTopicClient();
        }
        private ITopicClient createTopicClient()
        {
            //İlgili topic var mı yok mu kontrolü sağlandıktan sonra yeni bir topic yaratılır ve management client üzerinden topic yok ise topic oluşturulur
            if (topicClient ==null || topicClient.IsClosedOrClosing)
            {
                topicClient = new TopicClient(EventBusConfig.EventBusConnectionString, EventBusConfig.DefaultTopicName, RetryPolicy.Default);
            }

            //Ensure that topic already exist
            if (!managementClient.TopicExistsAsync(EventBusConfig.DefaultTopicName).GetAwaiter().GetResult())
                managementClient.CreateTopicAsync(EventBusConfig.DefaultTopicName).GetAwaiter().GetResult();

            return topicClient;
        }

        public override void Publish(IntegrationEvent @event)
        {
            //Bir mesajı alacak ve asb ye gönderecek
            //mesaj içindeki Label property si mesajın hangi etiketle gönderileceği yani eventName ile gönderilir.

            var eventName = @event.GetType().Name; //Örn: OrderCreatedIntegrationEvent
            //EventName sonundaki IntegrationEvent ibaresini kaldırmak istiyorduk.. BaseEventBus class ında ProvessEventName class ına istediğimiz yapıyı işletelim
            eventName = ProcessEventName(eventName); // Örn: OrderCreated
            var eventStr = JsonConvert.SerializeObject(@event); //@event i serileştirdik ve UTF8 yardımı ile byte[] türünde body ye ekledik
            var bodyArr = Encoding.UTF8.GetBytes(eventStr);
            var message = new Message()
            {
                MessageId = Guid.NewGuid().ToString(),
                Body = bodyArr,
                Label = eventName,

            };
            topicClient.SendAsync(message).GetAwaiter().GetResult();
        }

        public override void Subscribe<T, TH>()
        {
            //T:IntegrationEvent, TH:IIntegrationEventHandler
            //SubscriptionManager yardımı ile bir event e subscribe olunur.
            var eventName = typeof(T).Name;
            if (!SubsManager.HasSubscriptionsForEvent(eventName))//subs yok ise...
            {
                var subscriptionClient = CreateClientSubscriptionClientIfNotExist(eventName);
                //Buraya kadar şu işlemleri yaptık. Topic var, default rule sildik. kendi rule umuzu oluşturduk.
                //Şimdi ise burayı dinlemeye başlamalıyız. Bir registeration işlemi yapmalıyız
                RegisterSubscriptionClientMessageHandler(subscriptionClient);
            }
            logger.LogInformation("Subscribing to event {EventName} with {EventHandler}", eventName, typeof(TH).Name);//işlemi bitirdikten sonra loglar
            SubsManager.AddSubscription<T, TH>(); // En son subscriptionlarımızı ekliyoruz
        }

        public override void UnSubscribe<T, TH>()
        {
            var eventName = typeof(T).Name;
            try
            {
                //subscription will be there but we dont subscribe
                var subscriptionClient = CreateSubscriptionClient(eventName);

                subscriptionClient.RemoveRuleAsync(eventName).GetAwaiter().GetResult();
            }
            catch (MessagingEntityNotFoundException)
            {
                logger.LogWarning("The messaging entity {eventName} could not be found", eventName);
            }
            logger.LogInformation("UnSubscribing from event {EventName}", eventName);
            SubsManager.RemoveSubscription<T, TH>();
        }

        private void RegisterSubscriptionClientMessageHandler(ISubscriptionClient subscriptionClient)
        {
            //message : IIntegrationEvent den türer.
            subscriptionClient.RegisterMessageHandler(
                async (message, token) =>
                {
                    var eventName = $"{message.Label}";
                    var messageData = Encoding.UTF8.GetString(message.Body); //Gelen mesajı byte[] den json a çevirdik.

                    //Complete the message so that it is not received again.
                    if (await ProcessEvent(ProcessEventName(eventName), messageData)) //Base de olan ProcessEvent sayesinde eventName ve mesaj datasını process ediyoruz.
                    {
                        await subscriptionClient.CompleteAsync(message.SystemProperties.LockToken);//işlemi tamamladığımı bildiriyorum ve kilitliyorum
                    }
                },
                new MessageHandlerOptions(ExceptionReceiverHandler) { MaxConcurrentCalls = 10, AutoComplete = false });
        }

        private Task ExceptionReceiverHandler(ExceptionReceivedEventArgs exceptionReceivedEventArgs)
        {
            //Mesajı concume ederken bir sorun oluşursa detaylarını logladık
            var ex = exceptionReceivedEventArgs.Exception;
            var context = exceptionReceivedEventArgs.ExceptionReceivedContext;

            logger.LogError(ex,"ERROR handling meesage : {ExceptionMessage} - Context :{@ExceptionContext}",ex.Message,context);
            return Task.CompletedTask;
        }

        private ISubscriptionClient CreateClientSubscriptionClientIfNotExist(string eventName)
        {
            var subClient = CreateSubscriptionClient(eventName);
            //management api yi kullanarak subscription olup olmadığını kontrol ederiz
            var exist = managementClient.SubscriptionExistsAsync(EventBusConfig.DefaultTopicName, GetSubName(eventName)).GetAwaiter().GetResult();
            if (!exist) //eğer gelen bir eventName ile subscription name yok ise management client yardımı ile yeni bir subscription oluştur.
            {
                managementClient.CreateSubscriptionAsync(EventBusConfig.DefaultTopicName, GetSubName(eventName)).GetAwaiter().GetResult();
                //Bir subscription yaratıldığında default rule yaratılır. O rule silinir ve gerekirse kendi rule larımızı yazarız.
                RemoveDefaultRule(subClient);
            }
            //Hangi kurala uyanlar bizim subscription ımıza gelmesini istiyorsak kendimiz bir rule yazmalıyız
            CreateRuleNotExist(ProcessEventName(eventName), subClient);//kendi rule umuzu tanımladık
            return subClient;
        }

        private void CreateRuleNotExist(string eventName,ISubscriptionClient subscriptionClient)
        {
            //Rule ismi event name ile aynı olacak. 
            bool ruleExist;
            try
            {
                var rule = managementClient.GetRuleAsync(EventBusConfig.DefaultTopicName, eventName, eventName).GetAwaiter().GetResult();
                ruleExist = rule != null;
            }
            catch (MessagingEntityNotFoundException)
            {
                //Azure Management Client doens't have ruleExist method
                ruleExist = false;
            }
            //Eğer gönderdiğim event name e sahip herhangi bir rule yok ise yeni bir rule eklenir.
            if (!ruleExist)
            {
                //Dışarıdan gönderilen bir event. Örneğin OrderCreated event i geldiğinde bu subscription altında rule olarak filter ve name olarak benim gönderdiğim event ismi ile uyumlu olmasını beklerim.
                subscriptionClient.AddRuleAsync(new RuleDescription
                {
                    Filter = new CorrelationFilter { Label = eventName},
                    Name = eventName
                }).GetAwaiter().GetResult();

            }
        }

        private void RemoveDefaultRule(SubscriptionClient subscriptionClient)
        {
            try
            {
                //subsclient yardımı ile oluşan rule u gider ve siler
                subscriptionClient
                    .RemoveRuleAsync(RuleDescription.DefaultRuleName)
                    .GetAwaiter().GetResult();
            }
            catch (MessagingEntityNotFoundException)
            {
                logger.LogWarning("The messaging entity {DefaultRuleName} could not be found.", RuleDescription.DefaultRuleName);
            }
        }

        private SubscriptionClient CreateSubscriptionClient(string eventName)
        {
            return new SubscriptionClient(EventBusConfig.EventBusConnectionString, EventBusConfig.DefaultTopicName,GetSubName(eventName));
        }

        public override void Dispose()
        {
            base.Dispose();
            //Garbage collector tarafına destek için dispose ettiğmiz nesneleri kapatıp null a eşitliyoruzz
            topicClient.CloseAsync().GetAwaiter().GetResult();
            managementClient.CloseAsync().GetAwaiter().GetResult();
            topicClient = null;
            managementClient = null;
        }
    }
}
