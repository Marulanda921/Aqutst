namespace TCP_AQUTEST.Models.Kafka
{
    /// <summary>
    /// Representa la configuracion de kafka.
    /// </summary>
    public class KafkaSettings
    {
        public string? BootstrapServers { get; set; }
        public string? GroupId { get; set; }
        public string? Topic { get; set; }
    }
}
