using Confluent.Kafka;
using Microsoft.Extensions.Options;
using TCP_AQUTEST.Infraestructure.Interfaz;
namespace TCP_AQUTEST.Models.Kafka
{


    /// <summary>
    /// Inyeccion de dependendencias
    /// Constructor para poder almacenar las inyecciones de dependencias pertinentes
    /// validar la configuracion de el programa para que cumpla con las directrices pedidas
    /// </summary>
    public class KafkaProducer : IKafkaProducer, IDisposable
    {
        private readonly IProducer<Null, string> _producer;
        private readonly ILogger<KafkaProducer> _logger;

        public KafkaProducer(IOptions<KafkaSettings> settings, ILogger<KafkaProducer> logger)
        {
            _logger = logger;
            //
            var config = new ProducerConfig
            {
                BootstrapServers = settings.Value.BootstrapServers,
                EnableDeliveryReports = true,
                RetryBackoffMs = 1000,
                MessageTimeoutMs = 3000
            };
            _producer = new ProducerBuilder<Null, string>(config).Build();
        }




        /// <summary>
        /// se generan las tateas de manera asyncrona - topico / mensaje
        /// el reuslado es la espera del producer asyncrono que nos trae el mensaje
        /// si falla el envio del mensaje
        /// </summary>
        /// <param name="topic"></param>
        /// <param name="message"></param>
        /// <returns></returns>

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
