using EventBus.Base;
using EventBus.Base.Abstraction;
using EventBus.Factory;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.HttpsPolicy;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi.Models;
using PaymentService.Api.IntegrationEvents.EventHandlers;
using PaymentService.Api.IntegrationEvents.Events;
using RabbitMQ.Client;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace PaymentService.Api
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {

            services.AddControllers();
            services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("v1", new OpenApiInfo { Title = "PaymentService.Api", Version = "v1" });
            });

            //Aşağıdaki kodları ekledik
            services.AddLogging(configure => configure.AddConsole()); //Console üzerinde logları görmeye yarar
            services.AddTransient<OrderStartedIntegrationEventHandler>(); //BaseEventBus class içinde handler nesnesini alırken. ServiceProvider.GetService(subscription.HandlerType) metodu kullanılıyor. handler ın getirdiği servis tipinin handle metodunu tetikleyecek. Bu yüzden burada ayağa kaldırarak sisteme inject edilir. Service collection üzerinden bir handler create edilir.
            services.AddSingleton<IEventBus>(sp => {
                EventBusConfig config = new()
                {
                    ConnectionRetryCount = 5,
                    EventNameSuffix = "IntegrationEvent",
                    SubscriberClientAppName = "PaymentService",
                    EventBusType = EventBusType.RabbitMQ
                };

                return EventBusFactory.Create(config, sp); //EventBusFactory ile rabbit mq konfigurasyonu yaptık. Servisin bilgilerini ve config ayarlarını gönderdik.
            });
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
                app.UseSwagger();
                app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "PaymentService.Api v1"));
            }

            app.UseHttpsRedirection();

            app.UseRouting();

            app.UseAuthorization();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
            });

            //Aşağıdaki kodları ekledik
            IEventBus eventBus = app.ApplicationServices.GetRequiredService<IEventBus>(); //sistem içinden IEventBus istedik. Configure service içinde tanımladığımız IEventBus ve içindeki Config ayarları sistemde RabbitMq üzerinde configurasyonu gerçekleştirir ve buraya döner.
            eventBus.Subscribe<OrderStartedIntegrationEvent, OrderStartedIntegrationEventHandler>(); //yukarıda üretilen eventBus a OrderStartedIntegrationEvent i dinlemeye başla ve sisteme haber ver. Haber verirken ise OrderStartedIntegrationEventHandler ı kullan diyoruz
        }
    }
}
