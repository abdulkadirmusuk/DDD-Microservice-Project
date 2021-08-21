using Polly;
using RabbitMQ.Client;
using RabbitMQ.Client.Exceptions;
using System;
using System.Net.Sockets;

namespace EventBus.RabbitMQ
{
    public class RabbitMQPersistentConnection : IDisposable
    {
        private readonly IConnectionFactory connectionFactory;
        private readonly int retryCount;
        private IConnection connection;//rabbit mq connection nesnesi
        private object lock_object = new object(); //persistent connection dan gelen kilitleme objesi
        private bool _disposed;

        public RabbitMQPersistentConnection(IConnectionFactory connectionFactory, int retryCount=5)
        {
            this.connectionFactory = connectionFactory;
            this.retryCount = retryCount;
        }
        public bool IsConnected => connection != null && connection.IsOpen; //rabbit mq bağlı ve açık ise true
        public IModel CreateModel() //rabbit mq bağlanmak için model oluştur
        {
            return connection.CreateModel();
        }
        public void Dispose()
        {
            _disposed = true;
            connection.Dispose();
        }
        public bool TryConnect()
        {
            //rabbit mq bağlantısı sağlama
            lock (lock_object)//bağlanırken rabbit mq yu kilitler. Bir önceki işlemin bitmesini bekler
            {
                //Policy: Polly nuget package
                var policy = Policy.Handle<SocketException>()
                    .Or<BrokerUnreachableException>()
                    .WaitAndRetry(retryCount, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)), (ex, time) =>
                        {
                        }
                    );
                policy.Execute(() =>
                {
                    connection = connectionFactory.CreateConnection(); //policy uygularken socket exception ve broker unreachable gibi hatalar alırsa bağlanmayı yeniden dene demektri.
                });
                if (IsConnected)
                {
                    connection.ConnectionShutdown += Connection_ConnectionShutdown;
                    connection.CallbackException += Connection_CallbackException;
                    connection.ConnectionBlocked += Connection_ConnectionBlocked;
                    //log
                    return true;
                }
                return false;
            }

        }

        private void Connection_ConnectionBlocked(object sender, global::RabbitMQ.Client.Events.ConnectionBlockedEventArgs e)
        {
            if (!_disposed) return; //disposed edilmediyse tekrar denemek lazım
            TryConnect();
        }

        private void Connection_CallbackException(object sender, global::RabbitMQ.Client.Events.CallbackExceptionEventArgs e)
        {
            if (!_disposed) return; //disposed edilmediyse tekrar denemek lazım
            TryConnect();
        }

        private void Connection_ConnectionShutdown(object sender, ShutdownEventArgs e)
        {
            //log conneciton show down
            if (!_disposed) return; //disposed edilmediyse tekrar denemek lazım
            TryConnect(); //bağlantı koğtuğu zaman denemeye devam eder (retryCount sayısı kadar deneme yapar!)
        }
    }
}
