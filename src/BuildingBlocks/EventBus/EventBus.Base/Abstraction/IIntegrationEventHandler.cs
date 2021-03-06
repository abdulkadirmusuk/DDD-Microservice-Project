using EventBus.Base.Event;
using System.Threading.Tasks;

namespace EventBus.Base.Abstraction
{
    //Gelen eventlerin handle edilmesi için kullanılacak
    public interface IIntegrationEventHandler<TIntegrationEvent> : IntegrationEventHandler 
        where TIntegrationEvent: IntegrationEvent
    {
        Task Handle(TIntegrationEvent @event); 
    }

    public interface IntegrationEventHandler
    {
        //mark up interface
    }
}
