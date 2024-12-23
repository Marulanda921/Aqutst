using Confluent.Kafka;
using Confluent.Kafka.Admin;
using Microsoft.Extensions.Options;
using TCP_AQUTEST.Infraestructure.Interfaz;
using TCP_AQUTEST.Models.Kafka;

namespace TCP_AQUTEST.Services
{

    /// <summary>
    /// Clase que representa un consumidor de Kafka.
    /// Esta clase se encarga de consumir mensajes de un tópico de Kafka y
    /// almacenarlos en una base de datos.
    /// </summary>
    public class KafkaConsumer : BackgroundService
    {
        private readonly IConsumer<string, string> _consumer;
        private readonly IOptions<KafkaSettings> _settings;
        private readonly IBdService _db;

        /// <summary>
        /// Constructor de la clase.
        /// Inicializa una nueva instancia de la clase KafkaConsumer.
        /// Este constructor recibe un objeto de configuración de Kafka y una
        /// instancia de la interfaz IBdService.
        /// </summary>
        /// <param name="settings"></param>
        /// <param name="database"></param>
        public KafkaConsumer(IOptions<KafkaSettings> settings, IBdService database)
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

        /// <summary>
        /// Método que crea un tópico en Kafka si no existe.
        /// Este método se encarga de crear un tópico en Kafka si no existe.
        /// Si el tópico ya existe, el método no hace nada.
        /// Si ocurre un error al crear el tópico, el método imprime un mensaje
        /// de error en la consola.
        /// Este método no retorna ningún valor.
        /// </summary>
        private void CreateTopicIfNotExists()
        {
            //Crear nueva configuracion para el administrador
            var adminConfig = new AdminClientConfig
            {
                BootstrapServers = _settings.Value.BootstrapServers
            };
            //
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
                if (ex.Error.Code == Confluent.Kafka.ErrorCode.TopicAlreadyExists)
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

        /// <summary>
        /// Método que inicia el consumidor de Kafka.
        /// Este método inicia el consumidor de Kafka y comienza a consumir
        /// mensajes del tópico configurado en el objeto de configuración.
        /// Este método no retorna ningún valor.
        /// </summary>
        /// <param name="stoppingToken"></param>
        /// <returns></returns>
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

        /// <summary>
        /// Método que procesa un mensaje de Kafka.
        /// Este método recibe un mensaje de Kafka y lo almacena en una base de
        /// datos.
        /// Este método no retorna ningún valor.
        /// </summary>
        /// <param name="message"></param>
        /// <returns></returns>
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