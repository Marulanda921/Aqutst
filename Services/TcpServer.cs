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
using System.Collections;
namespace TCP_AQUTEST.Services
{
    /// <summary>
    /// Clase que se encarga de gestionar el servidor TCP y las conexiones entrantes.
    /// Hereda de BackgroundService, que es una clase de ASP.NET Core que implementa IHostedService
    /// y se encarga de ejecutar un servicio en segundo plano.
    /// configuración para leer valores de un archivo JSON 
    /// toma cuatro parámetros. Cada uno de estos parámetros es un servicio o una interfaz que probablemente será inyectado en la clase TcpServer
    /// </summary>
    public class TcpServer : BackgroundService
    {
        private readonly IKafkaProducer _kafkaProducer;
        private readonly IOptions<KafkaSettings> _settings;
        private readonly ILogger<TcpServer> _logger;
        private IMongoCollection<BsonDocument> _collection;
        private readonly IBdService _db;
        public static readonly string PortTCP = new ConfigurationBuilder().AddJsonFile("appsettings.json").Build().GetSection("TCP")["Port"];


        /// <summary>
        /// Constructor de la clase TcpServer
        /// Inyecta las dependencias necesarias para el servidor TCP.
        /// Estas dependencias son un productor de Kafka, la configuración de Kafka, un logger y un servicio de base de datos.
        /// El productor de Kafka se utiliza para enviar mensajes a un tópico de Kafka, la configuración de Kafka se utiliza para leer la configuración de Kafka desde un archivo JSON,
        /// el logger se utiliza para registrar mensajes en los logs y el servicio de base de datos se utiliza para insertar documentos en una base de datos.
        /// El constructor asigna las dependencias a las variables de la clase para que puedan ser utilizadas en otros métodos
        /// </summary>
        /// <param name="kafkaProducer"></param>
        /// <param name="settings"></param>
        /// <param name="logger"></param>
        /// <param name="database"></param>
        public TcpServer(IKafkaProducer kafkaProducer,
            IOptions<KafkaSettings> settings,
            ILogger<TcpServer> logger, IBdService database)
        {
            _db = database;
            _kafkaProducer = kafkaProducer;
            _settings = settings;
            _logger = logger;
        }

        /// <summary>
        /// Método que se ejecuta en segundo plano y se encarga de iniciar el servidor TCP y escuchar las conexiones entrantes.
        /// Este método se ejecuta cuando el servicio se inicia y se ejecuta en segundo plano hasta que el servicio se detiene.
        /// El método acepta un token de cancelación que se utiliza para detener el servicio de forma segura.
        /// El método crea un servidor TCP que escucha en un puerto específico y acepta conexiones entrantes.
        /// Cuando se acepta una conexión, se procesa en segundo plano y se continúa escuchando para nuevas conexiones.
        /// El método registra mensajes informativos en los logs para indicar que el servidor ha comenzado a escuchar y que un cliente se ha conectado.
        /// Si ocurre un error al aceptar una conexión o al procesar un cliente, se registra un mensaje de error en los logs.
        /// Cuando el servicio se detiene, se detiene el servidor TCP y se registra un mensaje informativo en los logs.
        /// </summary>
        /// <param name="stoppingToken"></param>
        /// <returns></returns>
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var server = new TcpListener(IPAddress.Any, int.Parse(PortTCP));
            try
            {
                server.Start();
                var localEndPoint = server.LocalEndpoint as IPEndPoint;
                if (localEndPoint != null)
                {
                    string localIPAddress = GetLocalIPAddress();
                    _logger.LogInformation($"Servidor TCP iniciado en IP: {localIPAddress} y puerto: {localEndPoint.Port}");
                }
                while (!stoppingToken.IsCancellationRequested)
                {
                    try
                    {
                        var client = await server.AcceptTcpClientAsync(stoppingToken);
                        _logger.LogInformation($"Cliente conectado desde: {(client.Client.RemoteEndPoint as IPEndPoint)?.Address}");
                        _ = Task.Run(async () =>
                        {
                            try
                            {
                                await ProcessClientAsync(client, stoppingToken);
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError($"Error procesando cliente: {ex.Message}");
                            }
                        }, stoppingToken);
                    }
                    catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                    {
                        break;
                    }
                    catch (Exception ex)
                    {
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


        /// <summary>
        /// Método que obtiene la dirección IP local de la máquina.
        /// El método recorre todas las interfaces de red disponibles y busca la primera dirección IPv4 que no sea de loopback.
        /// Si encuentra una dirección válida, la devuelve como un string.
        /// Si no encuentra una dirección válida, devuelve un mensaje indicando que la dirección IP no fue encontrada.
        /// </summary>
        /// <returns></returns>
        private string GetLocalIPAddress()
        {
            foreach (var networkInterface in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (networkInterface.OperationalStatus == OperationalStatus.Up &&
                    (networkInterface.NetworkInterfaceType == NetworkInterfaceType.Wireless80211 ||
                     networkInterface.NetworkInterfaceType == NetworkInterfaceType.Ethernet))
                {var ipProperties = networkInterface.GetIPProperties();
                    foreach (var address in ipProperties.UnicastAddresses)
                    {
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



        /// <summary>
        /// Método que se encarga de procesar un cliente TCP.
        /// El método recibe un cliente TCP y un token de cancelación.
        /// El método lee los datos enviados por el cliente y los procesa.
        /// Los datos leídos se convierten a un string hexadecimal y se registran en los logs.
        /// Los datos se procesan para obtener información relevante y se registran en los logs.
        /// Los datos procesados se convierten a un string JSON y se envían a un tópico de Kafka.
        /// Los datos procesados se convierten a un string hexadecimal y se envían al cliente.
        /// Si ocurre un error al leer los datos, procesar los datos o enviar los datos, se registra un mensaje de error en los logs.
        /// Al finalizar el procesamiento, se cierra la conexión con el cliente.
        /// </summary>
        /// <param name="client"></param>
        /// <param name="stoppingToken"></param>
        /// <returns></returns>
        public async Task ProcessClientAsync(TcpClient client, CancellationToken stoppingToken)
        {
            using var stream = client.GetStream();
            var buffer = new byte[1024];

            try
            {
                int bytesRead;
                while ((bytesRead = await stream.ReadAsync(buffer, stoppingToken)) > 0)
                {
                    var receivedData = new byte[bytesRead];
                    Array.Copy(buffer, receivedData, bytesRead);
                    string hexString = string.Join(" ", receivedData.Select(b => b.ToString("X2")));
                    _logger.LogInformation($"Datos leídos (hex): {hexString}");

                    ReadSensor grSensor = procesDataForSensorGeneric(hexString);
                    await _db.InsertDocument("ReadSensor", JsonConvert.SerializeObject(grSensor));

                    ReadSensorFormat rSensor = ProcessData(receivedData);
                    var jsonReadSensor = JsonConvert.SerializeObject(rSensor);

                    _logger.LogInformation($"Datos procesados (json): {jsonReadSensor}");
                    await _kafkaProducer.ProduceAsync(_settings.Value.Topic, jsonReadSensor);

                    DateTime now = DateTime.Now;
                    string formattedDate = now.ToString("yyyyMMddHHmmss");

                    string responseString = rSensor.PlotSize  + rSensor.PlotVersion  + rSensor.EncodeType  + rSensor.PlotIntegrity +
                         rSensor.AquaSerial  + rSensor.Master  + rSensor.SensorCode  + rSensor.Channel +
                         rSensor.SystemComand  + rSensor.ResponseCode  + formattedDate  + rSensor.Nut;

                    string AddSpacesEveryTwoCharacters(string input)
                    {
                        return string.Concat(input.Select((c, i) => i > 0 && i % 2 == 0 ? " " + c : c.ToString()));
                    }
                    string formattedResponseString = AddSpacesEveryTwoCharacters(responseString);

                    _logger.LogInformation($"Datos a enviar (str): {responseString}");
                    _logger.LogInformation($"Bytes a enviar (hex): {formattedResponseString}");

                    try
                    {
                        byte[] responseHexBytes = formattedResponseString
                            .Trim()                       
                            .Split(' ')               
                            .Where(hex => !string.IsNullOrEmpty(hex)) 
                            .Select(hex => Convert.ToByte(hex, 16))
                            .ToArray();
                        await stream.WriteAsync(responseHexBytes, 0, responseHexBytes.Length);
                    }
                    catch (FormatException ex)
                    {
                        _logger.LogError($"Error al convertir a bytes: {ex.Message}");
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

        /// <summary>
        /// Método que procesa los datos recibidos del cliente y los convierte en un objeto ReadSensorFormat.
        /// El método recibe un array de bytes con los datos recibidos del cliente.
        /// El método procesa los datos para obtener información relevante y la almacena en un objeto ReadSensorFormat.
        /// El objeto ReadSensorFormat contiene la información obtenida de los datos recibidos.
        /// Si ocurre un error al procesar los datos, se registra un mensaje de error en los logs y se devuelve un objeto ReadSensorFormat vacío.
        /// Al finalizar el procesamiento, se devuelve el objeto ReadSensorFormat con la información obtenida de los datos recibidos.
        /// </summary>
        /// <param name="messageData"></param>
        /// <returns></returns>
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

                var responseCode = messageData.Length >= 16 ?
                    (BitConverter.ToString(messageData.Skip(15).Take(1).ToArray()).Replace("-", "")) : null;

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



        /// <summary>
        /// Método que convierte un string hexadecimal en un valor double.
        /// El método recibe un string hexadecimal y lo convierte en un valor double.
        /// El método convierte el string hexadecimal en un array de bytes y luego lo convierte en un valor float.
        /// Luego, convierte el valor float en un valor double y lo devuelve.
        /// Si el sistema es little-endian, invierte los bytes antes de convertirlos a float.
        /// </summary>
        /// <param name="hexString"></param>
        /// <returns></returns>
        public static double ConvertHexToDouble(string hexString)
        {
            byte[] bytes = new byte[4];
            for (int i = 0; i < 4; i++)
            {
                bytes[i] = Convert.ToByte(hexString.Substring(i * 2, 2), 16);
            }
            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(bytes);
            }
            float floatValue = BitConverter.ToSingle(bytes, 0);
            return Convert.ToDouble(floatValue);
        }



        /// <summary>
        /// Método que procesa los datos recibidos del cliente y los convierte en un objeto ReadSensor.
        /// El método recibe un string con los datos recibidos del cliente.
        /// El método procesa los datos para obtener información relevante y la almacena en un objeto ReadSensor.
        /// El objeto ReadSensor contiene la información obtenida de los datos recibidos.
        /// Si ocurre un error al procesar los datos, se registra un mensaje de error en los logs y se devuelve un objeto ReadSensor vacío.
        /// Al finalizar el procesamiento, se devuelve el objeto ReadSensor con la información obtenida de los datos recibidos.
        /// </summary>
        /// <param name="msg"></param>
        /// <returns></returns>
        public ReadSensor procesDataForSensorGeneric(string msg)
        {
            try
            {
                var realTime = DateTime.Now;

                return new ReadSensor()
                {
                    ReadBytes = msg,
                    DateReceipt = realTime
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




