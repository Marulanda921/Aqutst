namespace TCP_AQUTEST.Infraestructure.Interfaz
{
    /// <summary>
    /// Interfaz del productor de Kafka, recibe el topic y el mensaje
    /// y lo envia a un servidor de Kafka
    /// </summary>
    public interface IKafkaProducer
    {
        Task ProduceAsync(string topic, string message);
    }


}
