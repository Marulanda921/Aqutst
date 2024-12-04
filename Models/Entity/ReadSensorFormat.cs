namespace TCP_AQUTEST.Models.Entity
{

    public class ReadSensorFormat
    {
        public string? PlotSize { get; set; }
        public string? PlotVersion {get; set;}
        public string? EncodeType { get; set; }
        public string  PlotIntegrity { get; set; }
        public string? AquaSerial {get; set;}
        public string? Master { get; set; }
        public string? SensorCode { get; set; }
        public string? Channel { get; set; }
        public string? SystemComand { get; set; }
        public DateTime? DateReadSensor { get; set; }
        public DateTime? DateReadService { get; set; }
        public int? ResponseCode { get; set; }
        public int? Nut { get; set; }
        public string? Alert { get; set; }
        public double? TransmissionValue { get; set; }
        public string? TypeMessage { get; set; }
    }
}
