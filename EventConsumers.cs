
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using ServiceStack.Redis;
using MassTransit;
using Wopr.Core;

namespace Wopr.Unicorn {

    public class Emotes{
        public const string Thinking = "\uD83E\uDD14";
        public const string ThumbsUp = "\uD83D\uDC4D";
        public const string ThumbsDown =  "\uD83D\uDC4E";
        public const string WhiteCheckMark = "\u2705";
        public const string NoEntry = "\u26D4";
    }

    //"{\"predictions\":[[\"unicorn\",0.9814049601554871],[\"notunicorn\",0.018595080822706223]]}"
    public class ClassifyResult{
        public object[][] predictions { get; set; }
    }

    public class ImageDownloadedConsumer : IConsumer<ImageDownloaded> {
        
        IRedisClientsManager redis;
        private static readonly HttpClient http = new HttpClient();

        public ImageDownloadedConsumer(IRedisClientsManager redis){
            this.redis = redis;
        }

        public async Task Consume(ConsumeContext<ImageDownloaded> context){
            var reaction = ProcessImage(context.Message);
            if(reaction != null)
                await context.Publish(reaction);
        }

         public AddReaction ProcessImage(ImageDownloaded image){
            using(var client = redis.GetClient()){
                string watchkey = $"{RedisPaths.WatchedContent}:{image.MessageId}";
                var result = http.GetAsync($"http://detector:5000/wopr?id={image.MessageId}").Result;
                Console.WriteLine($"{image.MessageId} - {result.StatusCode}");                
                if(result.IsSuccessStatusCode){
                    var json = result.Content.ReadAsStringAsync().Result;
                    client.SetRangeInHash(watchkey, new KeyValuePair<string,string>[]{
                        new KeyValuePair<string, string>("status", "seen"),
                        new KeyValuePair<string, string>("result", json)
                    });

                    var classify = JsonSerializer.Deserialize<ClassifyResult>(json);

                    string classname = Convert.ToDouble(classify.predictions[0][1].ToString()) > Convert.ToDouble(classify.predictions[1][1].ToString()) ? classify.predictions[0][0].ToString() : classify.predictions[1][0].ToString();
                    return new AddReaction(){
                        Timestamp = DateTime.UtcNow,
                        MessageId = image.MessageId,
                        ChannelId = image.ChannelId,
                        Emote = classname == "unicorn" ? Emotes.WhiteCheckMark : Emotes.NoEntry
                    };

                } else {
                    return null;
                }
            }
        }
    }

    public class AddReactionConsumer : IConsumer<AddReaction> {
        
        IRedisClientsManager redis;
        
        public AddReactionConsumer(IRedisClientsManager redis){
            this.redis = redis;
        }

        public Task Consume(ConsumeContext<AddReaction> context){
            return Task.CompletedTask;
        }
    }

    public class RemoveReactionConsumer : IConsumer<RemoveReaction> {
        
        IRedisClientsManager redis;
        
        public RemoveReactionConsumer(IRedisClientsManager redis){
            this.redis = redis;
        }

        public Task Consume(ConsumeContext<RemoveReaction> context){
            return Task.CompletedTask;
        }
    }

    public class RemoveAllReactionsConsumer : IConsumer<RemoveAllReactions> {
        
        IRedisClientsManager redis;

        public RemoveAllReactionsConsumer(IRedisClientsManager redis){
            this.redis = redis;
        }

        public Task Consume(ConsumeContext<RemoveAllReactions> context){
            return Task.CompletedTask;            
        }
    }

}