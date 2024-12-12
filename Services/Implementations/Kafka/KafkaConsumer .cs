using Confluent.Kafka;
using Confluent.Kafka.Admin;
using Microsoft.Extensions.Options;
using TCP_AQUTEST.Models.Kafka;
using TCP_AQUTEST.Services.Contracts.DB;

namespace TCP_AQUTEST.Services.Implementations.Kafka
{
    public class KafkaConsumer : BackgroundService
    {
        private readonly IConsumer<string, string> _consumer;
        private readonly IOptions<KafkaSettings> _settings;
        private readonly IDBService _db;


        public KafkaConsumer(IOptions<KafkaSettings> settings, IDBService database)
        {
            _settings = settings;
            _db = database;

            var config = new ConsumerConfig
            {
                BootstrapServers = settings.Value.BootstrapServers,
                GroupId = settings.Value.GroupId,
                AutoOffsetReset = AutoOffsetReset.Earliest
            };
            _consumer = new ConsumerBuilder<string, string>(config).Build();
            CreateTopicIfNotExists();
        }

        private void CreateTopicIfNotExists()
        {
            var adminConfig = new AdminClientConfig
            {
                BootstrapServers = _settings.Value.BootstrapServers
            };

            using var adminClient = new AdminClientBuilder(adminConfig).Build();
            try
            {
                adminClient.CreateTopicsAsync(new TopicSpecification[]
                {
                    new TopicSpecification
                    {
                        Name = _settings.Value.Topic,
                        ReplicationFactor = 1,
                        NumPartitions = 1
                    }
                }).Wait();
            }
            catch (CreateTopicsException ex)
            {
                if (ex.Error.Code == ErrorCode.TopicAlreadyExists)
                {
                    Console.WriteLine($"El tema '{_settings.Value.Topic}' ya existe. Ignorando el error.");
                }
                else
                {
                    Console.WriteLine($"Error al crear el tema: {ex.Message}");
                }
            }
            catch (Exception ex)
            {
            }
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            try
            {
                _consumer.Subscribe(_settings.Value.Topic);
                while (!stoppingToken.IsCancellationRequested)
                {
                    try
                    {
                        var result = _consumer.Consume(stoppingToken);
                        if (result != null)
                        {
                            await ProcessMessage(result.Message.Value);
                        }
                    }
                    catch (ConsumeException ex)
                    {
                        Console.WriteLine($"Error consumiendo mensaje: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error fatal en el consumidor: {ex.Message}");
                throw;
            }
            finally
            {
                _consumer.Close();
            }
        }

        private async Task ProcessMessage(string message)
        {
            try
            {
                await _db.InsertDocument("ReadSensorFormat", message);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error procesando mensaje: {ex.Message}");
            }
        }
    }
}