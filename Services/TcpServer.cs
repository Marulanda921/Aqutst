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
using System.Diagnostics.Metrics;
using System.Threading.Channels;
using MongoDB.Bson;
using MongoDB.Driver;

namespace TCP_AQUTEST.Services
{
    //09/12/2024 - Alejandro Marulanda

    public class TcpServer : BackgroundService
    {
        private readonly IKafkaProducer _kafkaProducer;
        private readonly IOptions<KafkaSettings> _settings;
        private readonly ILogger<TcpServer> _logger;
        private IMongoCollection<BsonDocument> _collection;
        private readonly IBdService _db;


        


        //configuración para leer valores de un archivo JSON 
        public static readonly string PortTCP = new ConfigurationBuilder().AddJsonFile("appsettings.json").Build().GetSection("TCP")["Port"];


        //toma cuatro parámetros. Cada uno de estos parámetros es un servicio o una interfaz que probablemente será inyectado en la clase TcpServer
        public TcpServer(IKafkaProducer kafkaProducer,
            IOptions<KafkaSettings> settings,
            ILogger<TcpServer> logger, IBdService database)
        {
            _db = database;
            _kafkaProducer = kafkaProducer;
            _settings = settings;
            _logger = logger;
        }
                

        //Este método es el que se encarga de ejecutar el servidor TCP de manera asincrónica y gestionar las conexiones entrantes.
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            //escuchar las conexiones TCP entrantes en un puerto determinado. En este caso, el servidor escuchará en todas las interfaces de red de la máquina
            var server = new TcpListener(IPAddress.Any, int.Parse(PortTCP));

           
            try
            {
                // Inicia el servidor TCP para que comience a escuchar las conexiones entrantes.
                server.Start();

                //Obtiene el punto final (endpoint) local del servidor, que incluye la dirección IP y el puerto en el que está escuchando.
                var localEndPoint = server.LocalEndpoint as IPEndPoint;
                if (localEndPoint != null)
                {

                    //Se llama a este método para obtener la dirección IP local de la máquina.
                    string localIPAddress = GetLocalIPAddress();

                    //Registra un mensaje informativo en los logs, indicando que el servidor ha comenzado a escuchar en la dirección IP y puerto especificados.
                    _logger.LogInformation($"Servidor TCP iniciado en IP: {localIPAddress} y puerto: {localEndPoint.Port}");
                }

                //Este ciclo se ejecuta mientras el servicio no sea cancelado, lo que significa que sigue aceptando nuevas conexiones hasta que se le indique
                while (!stoppingToken.IsCancellationRequested)
                {
                    try
                    {
                        //Acepta de forma asincrónica una nueva conexión TCP entrante.
                        var client = await server.AcceptTcpClientAsync(stoppingToken);

                        //Registra un mensaje informativo con la dirección IP del cliente que se acaba de conectar.
                        _logger.LogInformation($"Cliente conectado desde: {(client.Client.RemoteEndPoint as IPEndPoint)?.Address}");

                        // Procesa el cliente en segundo plano y continúa escuchando
                        _ = Task.Run(async () =>
                        {
                            try
                            {
                                //ste es un método asincrónico que probablemente maneja la lógica de comunicación con el cliente, como leer y escribir datos en el flujo de la red.
                                await ProcessClientAsync(client, stoppingToken);
                            }
                            catch (Exception ex)
                            {
                                // Si ocurre un error al procesar el cliente, se captura y se registra el error.
                                _logger.LogError($"Error procesando cliente: {ex.Message}");
                            }
                        }, stoppingToken);
                    }
                    //servicio se cancela - Esto permite que el servidor se detenga limpiamente sin seguir aceptando nuevas conexiones.
                    catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                    {
                        // Salir limpiamente si se solicita cancelación
                        break;
                    }
                    catch (Exception ex)
                    {
                        //Si ocurre un error al intentar aceptar una conexión, se registra un mensaje de error, pero el ciclo sigue escuchando para nuevas conexiones.
                        _logger.LogError($"Error aceptando conexión: {ex.Message}");
                    }
                }
            }
            finally
            {
                server.Stop();
                _logger.LogInformation("Servidor TCP detenido.");
            }
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


                    //Sensor envio
                    ReadSensor grSensor = procesDataForSensorGeneric(hexString);
                    await _db.InsertDocument("ReadSensor", JsonConvert.SerializeObject((grSensor)));


                    foreach (var hex in hexValues)
                    {
                        if (byte.TryParse(hex, System.Globalization.NumberStyles.HexNumber, null, out byte value))
                        {
                            data.Add(value);
                        }
                    }

                    var messageData = data.ToArray();
                    ReadSensorFormat rSensor = ProcessData(messageData);
                        var jsonReadSensor = JsonConvert.SerializeObject(rSensor);
                        byte[] byteArray = Encoding.UTF8.GetBytes(jsonReadSensor);


                        _logger.LogInformation($"Datos procesados (json): {jsonReadSensor}");

                        // Enviar bytes directamente a Kafka sin serialización JSON
                        await _kafkaProducer.ProduceAsync(_settings.Value.Topic, jsonReadSensor);
                        await stream.WriteAsync(messageData, 0, messageData.Length);

                        data.RemoveRange(0, 20);
                    
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



        private ReadSensorFormat ProcessData(byte[] messageData)
        {
            try
            {
                var plotSize = messageData.Length >= 2
                    ? BitConverter.ToString(messageData.Take(2).ToArray()).Replace("-", "") : null;

                var plotVersion = messageData.Length >= 3
                    ? BitConverter.ToString(messageData.Skip(2).Take(1).ToArray()).Replace("-", "") : null;

                var encondeType = messageData.Length >= 4
                    ? BitConverter.ToString(messageData.Skip(3).Take(1).ToArray()).Replace("-", "") : null;

                var plotIntegrity = messageData.Length >= 7
                    ? BitConverter.ToString(messageData.Skip(4).Take(3).ToArray()).Replace("-", "") : null;

                var aquaSerial = messageData.Length >= 11
                    ? BitConverter.ToString(messageData.Skip(7).Take(4).ToArray()).Replace("-", "") : null;

                var master = messageData.Length >= 12
                    ? BitConverter.ToString(messageData.Skip(11).Take(1).ToArray()).Replace("-", "") : null;

                var sensorCode = messageData.Length >= 13
                    ? BitConverter.ToString(messageData.Skip(12).Take(1).ToArray()).Replace("-", "") : null;

                var channel = messageData.Length >= 14
                    ? BitConverter.ToString(messageData.Skip(13).Take(1).ToArray()).Replace("-", "") : null;

                var systemComand = messageData.Length >= 15
                    ? BitConverter.ToString(messageData.Skip(14).Take(1).ToArray()).Replace("-", "") : null;

                var responseCode = messageData.Length >= 16
                    ? int.Parse(BitConverter.ToString(messageData.Skip(15).Take(1).ToArray()).Replace("-", "")) : (int?)null;

                var dateReadService = DateTime.Now;

                var typeMessage = messageData.Length >= 4
                    ? BitConverter.ToString(messageData.Skip(3).Take(1).ToArray()).Replace("-", "") : null;

                DateTime? dateReadSensor = null;

                if (messageData.Length >= 23)
                {
                    var dateReadSensorStr = BitConverter.ToString(messageData.Skip(16).Take(7).ToArray()).Replace("-", "");
                    var dateFormat = "yyyyMMddHHmmss";

                    if (DateTime.TryParseExact(dateReadSensorStr, dateFormat,
                        System.Globalization.CultureInfo.InvariantCulture,
                        System.Globalization.DateTimeStyles.None, out var parsedDate))
                    {
                        dateReadSensor = parsedDate;
                    }
                }

                var nut = messageData.Length >= 27
                    ? int.Parse(BitConverter.ToString(messageData.Skip(23).Take(4).ToArray()).Replace("-", ""))
                    : (int?)null;


                var alert = messageData.Length >= 28
                    ? BitConverter.ToString(messageData.Skip(27).Take(1).ToArray())
                    : null;
                string transmissionHex = null;


                double? transmissionValue = null;

                if (messageData.Length >= 32)
                {
                    transmissionHex = BitConverter.ToString(messageData.Skip(28).Take(4).ToArray()).Replace("-", "");
                    transmissionValue = ConvertHexToDouble(transmissionHex);
                }

                var result = new ReadSensorFormat
                {
                    PlotSize = plotSize,
                    PlotVersion = plotVersion,
                    EncodeType = encondeType,
                    PlotIntegrity = plotIntegrity,
                    AquaSerial = aquaSerial,
                    Master = master,
                    Channel = channel,
                    SystemComand = systemComand,
                    SensorCode = sensorCode,
                    ResponseCode = responseCode,
                    DateReadSensor = dateReadSensor,
                    DateReadService = dateReadService,
                    Nut = nut,
                    Alert = alert,
                };
                if (!string.IsNullOrEmpty(transmissionHex) && transmissionValue.HasValue)
                {
                    result.TransmissionValue = transmissionValue.Value;
                    result.TypeMessage = typeMessage;
                }

                return result;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error al procesar los datos: {ex.Message}");
                return new ReadSensorFormat();
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




        public ReadSensor procesDataForSensorGeneric(string msg)
        {

            try
            {
                var realTime = DateTime.Now;

                return new ReadSensor()
                {
                    CompleteSensor = msg,
                    RealTime = realTime
                };

            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }


            
        }
    

    }
}




