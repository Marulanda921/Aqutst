using Microsoft.Extensions.Options;
using System.Net.Sockets;
using System.Net;
using TCP_AQUTEST.Infraestructure.Interfaz;
using TCP_AQUTEST.Models.Kafka;
using System.Text.Json;
using TCP_AQUTEST.Models.Entity;
using System.Net;
using System.Net.NetworkInformation;
using Confluent.Kafka;
using Newtonsoft.Json;
using System.Text;

namespace TCP_AQUTEST.Services
{
    public class TcpServer : BackgroundService
    {
        private readonly IKafkaProducer _kafkaProducer;
        private readonly IOptions<KafkaSettings> _settings;
        private readonly ILogger<TcpServer> _logger;

        public static readonly string PortTCP =
            new ConfigurationBuilder().AddJsonFile("appsettings.json").Build().GetSection("TCP")["Port"];

        public TcpServer(IKafkaProducer kafkaProducer,
            IOptions<KafkaSettings> settings,
            ILogger<TcpServer> logger)
        {
            _kafkaProducer = kafkaProducer;
            _settings = settings;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var server = new TcpListener(IPAddress.Any, int.Parse(PortTCP));
            server.Start();

            // Obtener la dirección IP local y el puerto del servidor
            var localEndPoint = server.LocalEndpoint as IPEndPoint;
            if (localEndPoint != null)
            {
                // Obtener la IP de la máquina (no la de '0.0.0.0')
                string localIPAddress = GetLocalIPAddress();
                _logger.LogInformation($"Servidor TCP iniciado en IP: {localIPAddress} y puerto: {localEndPoint.Port}");
            }
            else
            {
                _logger.LogInformation("Servidor TCP iniciado, pero no se pudo obtener la dirección IP.");
            }

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var client = await server.AcceptTcpClientAsync(stoppingToken);
                    _logger.LogInformation(
                        $"Cliente conectado desde: {(client.Client.RemoteEndPoint as IPEndPoint)?.Address}");

                    _ = ProcessClientAsync(client, stoppingToken); // Maneja al cliente de forma asíncrona
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Error en servidor TCP: {ex.Message}");
                }
            }

            // Detener el servidor cuando la aplicación se apaga
            server.Stop();
            _logger.LogInformation("Servidor TCP detenido.");
        }

        private string GetLocalIPAddress()
        {
            foreach (var networkInterface in NetworkInterface.GetAllNetworkInterfaces())
            {
                // Ignorar interfaces que no estén activas o no sean de tipo Ethernet o Wi-Fi
                if (networkInterface.OperationalStatus == OperationalStatus.Up &&
                    (networkInterface.NetworkInterfaceType == NetworkInterfaceType.Wireless80211 ||
                     networkInterface.NetworkInterfaceType == NetworkInterfaceType.Ethernet))
                {
                    var ipProperties = networkInterface.GetIPProperties();
                    foreach (var address in ipProperties.UnicastAddresses)
                    {
                        // Devolver la primera dirección IPv4 que no sea de loopback
                        if (address.Address.AddressFamily == AddressFamily.InterNetwork &&
                            !IPAddress.IsLoopback(address.Address))
                        {
                            return address.Address.ToString();
                        }
                    }
                }
            }

            return "IP no encontrada";
        }

        public async Task ProcessClientAsync(TcpClient client, CancellationToken stoppingToken)
        {
            using var stream = client.GetStream();
            var buffer = new byte[1024];
            var data = new List<byte>();

            try
            {
                int bytesRead;
                while ((bytesRead = await stream.ReadAsync(buffer, stoppingToken)) > 0)
                {
                    //_logger.LogInformation($"Datos recibidos (raw): {BitConverter.ToString(buffer, 0, bytesRead)}");

                    string hexString = System.Text.Encoding.ASCII.GetString(buffer, 0, bytesRead);
                    var hexValues = hexString.Split(' ', StringSplitOptions.RemoveEmptyEntries);

                    foreach (var hex in hexValues)
                    {
                        if (byte.TryParse(hex, System.Globalization.NumberStyles.HexNumber, null, out byte value))
                        {
                            data.Add(value);
                        }
                    }

                    while (data.Count >= 20)
                    {
                        var messageData = data.ToArray();

                        ReadSensor rSensor = ProcessData(messageData);
                        var jsonReadSensor = JsonConvert.SerializeObject(rSensor);
                        byte[] byteArray = Encoding.UTF8.GetBytes(jsonReadSensor);


                        _logger.LogInformation($"Datos procesados (json): {jsonReadSensor}");

                        // Enviar bytes directamente a Kafka sin serialización JSON
                        await _kafkaProducer.ProduceAsync(_settings.Value.Topic, jsonReadSensor);
                        await stream.WriteAsync(messageData, 0, messageData.Length);

                        data.RemoveRange(0, 20);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error TCP: {ex.Message}");
            }
            finally
            {
                client.Close();
            }
        }


        private ReadSensor ProcessData(byte[] messageData)
        {
            try
            {

                var typeMessage = (BitConverter.ToString(messageData.Skip(14).Take(1).ToArray()).Replace("-", ""));


                var responseCode = int.Parse(BitConverter.ToString(messageData.Skip(15).Take(1).ToArray()).Replace("-", ""));

                var dateReadService = DateTime.Now;



                var dateReadSensorStr = BitConverter.ToString(messageData.Skip(16).Take(7).ToArray()).Replace("-", "");

                var dateFormat = "yyyyMMddHHmmss";

                var dateReadSensor = DateTime.ParseExact(dateReadSensorStr, dateFormat,
                    System.Globalization.CultureInfo.InvariantCulture);

                var nut = int.Parse(BitConverter.ToString(messageData.Skip(23).Take(4).ToArray()).Replace("-", ""));

                var alert = BitConverter.ToString(messageData.Skip(27).Take(1).ToArray());

                var transmissionHex = (BitConverter.ToString(messageData.Skip(28).Take(4).ToArray()).Replace("-", ""));

                var transmissionValue = (ConvertHexToDouble(transmissionHex));
                
              


                return new ReadSensor
                {
                    ResponseCode = responseCode,
                    DateReadSensor = dateReadSensor,
                    DateReadService = dateReadService,
                    Nut = nut,
                    Alert = alert,
                    TransmissionValue = transmissionValue,
                    TypeMessage = typeMessage

                };


            }
            catch (Exception)
            {
                return new ReadSensor();
            }
        }


        public static double ConvertHexToDouble(string hexString)
        {
            // Convertir el string hex a bytes
            byte[] bytes = new byte[4];
            for (int i = 0; i < 4; i++)
            {
                bytes[i] = Convert.ToByte(hexString.Substring(i * 2, 2), 16);
            }

            // Si el sistema es little-endian, invertimos los bytes
            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(bytes);
            }

            // Primero convertimos a float (32 bits)
            float floatValue = BitConverter.ToSingle(bytes, 0);

            // Luego convertimos a double
            return Convert.ToDouble(floatValue);
        }
    

    }
}




