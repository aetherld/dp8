using System;
using System.Threading;
using System.Text;
using System.Text.Json;
using NATS.Client;
using StackExchange.Redis;

namespace RankCalculator
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("RankCalculator starting...");

            try
            {
                var redisConfig = ConfigurationOptions.Parse("localhost:6379");
                redisConfig.Password = "123456";
                var redis = ConnectionMultiplexer.Connect(redisConfig);
                var db = redis.GetDatabase();

                var natsOptions = ConnectionFactory.GetDefaultOptions();
                natsOptions.Url = "nats://user:123456@localhost:4222";

                using (var nats = new ConnectionFactory().CreateConnection(natsOptions))
                {
                    Console.WriteLine("Connected to Redis and NATS");

                    // Подписка на сообщения
                    var subscription = nats.SubscribeAsync(
                        subject: "valuator.processing.rank",
                        queue: "rank_calculator",
                        handler: (_, e) =>
                        {
                            try
                            {
                                var msg = JsonSerializer.Deserialize<MessageData>(
                                    Encoding.UTF8.GetString(e.Message.Data));

                                if (msg == null)
                                {
                                    Console.WriteLine("Received null message");
                                    return;
                                }

                                var text = db.StringGet(msg.TextKey);
                                if (text.IsNullOrEmpty)
                                {
                                    Console.WriteLine($"Text not found for key: {msg.TextKey}");
                                    return;
                                }

                                double rank = CalculateRank(text.ToString());
                                db.StringSet($"RANK-{msg.Id}", rank);

                                // Публикация события RankCalculated
                                var rankEvent = new
                                {
                                    EventType = "RankCalculated",
                                    TextId = msg.Id,
                                    Rank = rank
                                };

                                nats.Publish("events.rank", Encoding.UTF8.GetBytes(JsonSerializer.Serialize(rankEvent)));
                                Console.WriteLine($"Published RankCalculated for ID: {msg.Id}");

                                Console.WriteLine($"Processed ID: {msg.Id}, Rank: {rank}");
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"Error: {ex.Message}");
                            }
                        });

                    subscription.Start();
                    Console.WriteLine("Listening for messages...");

                    // Ожидание Ctrl+C
                    var waitHandle = new ManualResetEvent(false);
                    Console.CancelKeyPress += (_, _) => waitHandle.Set();
                    waitHandle.WaitOne();

                    subscription.Unsubscribe();
                }
                redis.Close();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Fatal error: {ex.Message}");
                Environment.Exit(1);
            }
        }

        private static double CalculateRank(string text)
        {
            double letterCount = text.Count(char.IsLetter);
            return text.Length == 0 ? 0 : 1 - letterCount / text.Length;
        }
    }

    public record MessageData(string Id, string TextKey);
}