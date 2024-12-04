using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using System.Text;
using System.Text.Json;
using Newtonsoft.Json;
using TCP_AQUTEST.Infraestructure.Interfaz;
using TCP_AQUTEST.Models.Entity;
using TCP_AQUTEST.Models.Kafka;

namespace TCP_AQUTEST.Controllers
{

    [ApiController]
    [Route("[controller]")]
    public class MessageController : ControllerBase
    {
        private readonly IKafkaProducer _kafkaProducer;
        private readonly IOptions<KafkaSettings> _settings;

        public MessageController(IKafkaProducer kafkaProducer, IOptions<KafkaSettings> settings)
        {
            _kafkaProducer = kafkaProducer;
            _settings = settings;
        }

        [HttpPost]
        public async Task<IActionResult> Post([FromBody] TcpMessage message)
        {
            //byte[] messageBytes = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject((message)));
            await _kafkaProducer.ProduceAsync(_settings.Value.Topic, JsonConvert.SerializeObject((message)));
            return Ok();
        }
    }
}

