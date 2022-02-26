using Newtonsoft.Json;
using System;

namespace EventBus.Base.Event
{
    //Servisler arası iletişimi sağlayacak ana yapı
    public class IntegrationEvent
    {
        [JsonProperty]
        public Guid Id { get; private set; } //private setter

        [JsonProperty]
        public DateTime CreatedDate { get; private set; }


        public IntegrationEvent()
        {
            Id = Guid.NewGuid();
            CreatedDate = DateTime.Now;
        }

        [JsonConstructor] //Json olarak dışardan gelen parametreyi almak için attribute
        public IntegrationEvent(Guid id, DateTime createdDate)
        {
            Id = id;
            CreatedDate = createdDate;
        }
    }
}
