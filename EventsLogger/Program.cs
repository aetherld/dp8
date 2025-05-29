using System;
using System.Threading;
using System.Text;
using System.Text.Json;
using NATS.Client;

namespace EventsLogger
{
    class Program
    {
        public static void Main(string[] args)
        {
            Console.WriteLine("EventsLogger started. Press Ctrl+C to exit.");

            try
            {
                var natsOptions = ConnectionFactory.GetDefaultOptions();
                natsOptions.Url = "nats://user:123456@localhost:4222";
                using (var nats = new ConnectionFactory().CreateConnection(natsOptions))
                {
                    Console.WriteLine("Connected to NATS server");

                    // Подписка на события RankCalculated
                    var rankSub = nats.SubscribeAsync("events.rank", (sender, e) =>
                    {
                        try
                        {
                            var msg = JsonSerializer.Deserialize<RankEvent>(
                                Encoding.UTF8.GetString(e.Message.Data));
                            Console.WriteLine($"[RANK] ID: {msg.TextId} | Value: {msg.Rank}");
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Error processing RankCalculated: {ex.Message}");
                        }
                    });
                    rankSub.Start();

                    // Подписка на события SimilarityCalculated
                    var similaritySub = nats.SubscribeAsync("events.similarity", (sender, e) =>
                    {
                        try
                        {
                            var msg = JsonSerializer.Deserialize<SimilarityEvent>(
                                Encoding.UTF8.GetString(e.Message.Data));
                            Console.WriteLine($"[SIMILARITY] ID: {msg.TextId} | Value: {msg.Similarity}");
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Error processing SimilarityCalculated: {ex.Message}");
                        }
                    });
                    similaritySub.Start();

                    Console.WriteLine("Subscribed to events. Waiting for messages...");

                    // Бесконечное ожидание
                    new ManualResetEvent(false).WaitOne();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Fatal error: {ex.Message}");
                Environment.Exit(1);
            }
        }
    }

    public record RankEvent(string TextId, double Rank);
    public record SimilarityEvent(string TextId, double Similarity);
}