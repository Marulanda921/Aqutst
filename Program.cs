using Microsoft.Extensions.Options;
using TCP_AQUTEST.Infraestructure.Interfaz;
using TCP_AQUTEST.Infraestructure;
using TCP_AQUTEST.Models.Kafka;
using TCP_AQUTEST.Services;

var builder = WebApplication.CreateBuilder(args);

// Configuraci�n de Kafka
builder.Services.Configure<KafkaSettings>(builder.Configuration.GetSection("Kafka"));

// Registro de servicios para Kafka y TCP
builder.Services.AddSingleton<IKafkaProducer, KafkaProducer>();
builder.Services.AddHostedService<TcpServer>(); // Registra TcpServer como servicio en segundo plano
builder.Services.AddHostedService<KafkaConsumer>(); // Registra KafkaConsumer como servicio en segundo plano

// Configuraci�n de controladores y Swagger
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Configuraci�n de Swagger para desarrollo
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

// Inicia la aplicaci�n
app.Run();
