using EventBus.Base.Event;

namespace EventBus.Base.Abstraction
{
    //Servislerin subscribtion işlemlerini, hangi event i subscribe edeceklerini söyleyecek event bus
    //AzureServiceBus ve RabbitMQ bu interface i kullanarak işlemlerini gerçekleştirecek 
    public interface IEventBus
    {
        void Publish(IntegrationEvent @event);//Service dışarıya bir event fırlatır
        //Subscribe olacak event ve o event i kimin handle edeceğini söyledik (Örn: OrderIntegrationCreatedEvent subsribtion verilecek ve handler türü verilecek(rabbitmq-asb) ve Handle methodu tetiklenecek)
        
        void Subscribe<T, TH>() where T : IntegrationEvent where TH : IIntegrationEventHandler<T>;
        void UnSubscribe<T, TH>() where T : IntegrationEvent where TH : IIntegrationEventHandler<T>;
    }
}
