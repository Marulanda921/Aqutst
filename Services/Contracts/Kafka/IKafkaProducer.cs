namespace TCP_AQUTEST.Services.Contracts.Kafka
{
    public interface IKafkaProducer
    {
        Task ProduceAsync(string topic, string message);
    }


}
