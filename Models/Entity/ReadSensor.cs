namespace TCP_AQUTEST.Models.Entity
{

    public class ReadSensor
    {
        public DateTime? DateReadSensor { get; set; }
        public DateTime? DateReadService { get; set; }
        public int? ResponseCode { get; set; }
        public int? Nut { get; set; }
        public string? Alert { get; set; }
        public double? TransmissionValue { get; set; }
        public string? TypeMessage { get; set; }

    }
}
