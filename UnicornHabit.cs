using System;
using System.Threading;
using System.Threading.Tasks;
using MassTransit;
using ServiceStack.Redis;
using Wopr.Core;

namespace Wopr.Unicorn
{
    public class UnicornHabit
    {
        RedisManagerPool redisPool;
        CancellationToken cancel;
        IBusControl bus;
        string rabbitToken;
        
        public UnicornHabit(Secrets secrets, CancellationToken cancel)
        {
            this.cancel = cancel;
            this.redisPool = new RedisManagerPool(secrets.RedisToken);
            this.rabbitToken = secrets.RabbitToken;
        }

        public void Start(){
            StartMassTransit().Wait();
        }

        public void Stop(){
            bus.Stop();
        }

        private Task StartMassTransit(){
            bus = Bus.Factory.CreateUsingRabbitMq(sbc =>
            {
                var parts = rabbitToken.Split('@');
                sbc.Host(new Uri(parts[2]), cfg => {
                    cfg.Username(parts[0]);
                    cfg.Password(parts[1]);
                });
                rabbitToken = string.Empty;

                sbc.ReceiveEndpoint("wopr:unicorn", ep =>
                {
                    ep.Consumer<ImageDownloadedConsumer>(()=>{return new ImageDownloadedConsumer(redisPool);});
                    ep.Consumer<AddReactionConsumer>(()=>{return new AddReactionConsumer(redisPool);});
                    ep.Consumer<RemoveReactionConsumer>(()=>{return new RemoveReactionConsumer(redisPool);});
                    ep.Consumer<RemoveAllReactionsConsumer>(()=>{return new RemoveAllReactionsConsumer(redisPool);});
                });
            });

            return bus.StartAsync(); // This is important!
        }
    }
}
