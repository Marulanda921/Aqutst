namespace TCP_AQUTEST.Infraestructure.Interfaz
{
    public interface IKafkaProducer
    {
        Task ProduceAsync(string topic, string message);
    }


}
