﻿using Confluent.Kafka;
using Microsoft.Extensions.Options;
using TCP_AQUTEST.Models.Kafka;
using TCP_AQUTEST.Services.Contracts.Kafka;
namespace TCP_AQUTEST.Services.Implementations.Kafka
{
    public class KafkaProducer : IKafkaProducer, IDisposable
    {
        private readonly IProducer<Null, string> _producer;
        private readonly ILogger<KafkaProducer> _logger;

        public KafkaProducer(IOptions<KafkaSettings> settings, ILogger<KafkaProducer> logger)
        {
            _logger = logger;
            var config = new ProducerConfig
            {
                BootstrapServers = settings.Value.BootstrapServers,
                EnableDeliveryReports = true,
                RetryBackoffMs = 1000,
                MessageTimeoutMs = 3000
            };
            _producer = new ProducerBuilder<Null, string>(config).Build();
        }

        public async Task ProduceAsync(string topic, string message)
        {
            try
            {
                var result = await _producer.ProduceAsync(topic,
                    new Message<Null, string> { Value = message });

                _logger.LogInformation(
                    $"Mensaje enviado a Kafka. Topic: {topic}, Partition: {result.Partition}, Offset: {result.Offset}");
            }
            catch (ProduceException<Null, byte[]> ex)
            {
                _logger.LogError($"Error al publicar mensaje: {ex.Message}");
                throw;
            }
        }
        public void Dispose()
        {
            _producer?.Dispose();
        }
    }
}
