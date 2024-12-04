using Confluent.Kafka;
using Confluent.Kafka.Admin;
using Microsoft.Extensions.Options;
using System.Text;
using TCP_AQUTEST.Models.Kafka;

namespace TCP_AQUTEST.Services
{
    public class KafkaConsumer : BackgroundService
    {
        private readonly IConsumer<string, string> _consumer;
        private readonly HttpClient _httpClient;
        private readonly IOptions<KafkaSettings> _settings;

        public KafkaConsumer(IOptions<KafkaSettings> settings)
        {
            _settings = settings;
            var config = new ConsumerConfig
            {
                BootstrapServers = settings.Value.BootstrapServers,
                GroupId = settings.Value.GroupId,
                AutoOffsetReset = AutoOffsetReset.Earliest
            };
            _consumer = new ConsumerBuilder<string, string>(config).Build();

            // Crear topic si no existe
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
                // Intentar crear el tema
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
                // Comprobamos si el código de error es "TopicAlreadyExists"
                if (ex.Error.Code == Confluent.Kafka.ErrorCode.TopicAlreadyExists)
                {
                    // El tema ya existe, ignoramos el error
                    Console.WriteLine($"El tema '{_settings.Value.Topic}' ya existe. Ignorando el error.");
                }
                else
                {
                    // Si el error no es por tema existente, mostramos el error
                    Console.WriteLine($"Error al crear el tema: {ex.Message}");
                }
            }
            catch (Exception ex)
            {
                // Manejo de cualquier otro tipo de error
                //Console.WriteLine($"Error inesperado: {ex.Message}");
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
                var content = new StringContent(message, Encoding.UTF8, "application/json");
                await _httpClient.PostAsync("http://tu-api-destino.com/endpoint", content);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error procesando mensaje: {ex.Message}");
            }
        }
    }
}