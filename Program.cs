using Microsoft.Extensions.Options;
using MongoDB.Driver;
using TCP_AQUTEST.Infraestructure.Interfaz;
using TCP_AQUTEST.Infraestructure;
using TCP_AQUTEST.Infraestructure.Utils;
using TCP_AQUTEST.Models.Kafka;
using TCP_AQUTEST.Services;

var builder = WebApplication.CreateBuilder(args);

//MongoDb conexion a la base de datos que establecimos
builder.Services.AddSingleton<IMongoDatabase>(sp =>
{
    var mongoClient = new MongoClient(builder.Configuration["MongoDB:Url"]);
    return mongoClient.GetDatabase(builder.Configuration["MongoDB:Database"]);
});

// Configuración de Kafka
builder.Services.Configure<KafkaSettings>(builder.Configuration.GetSection("Kafka"));

// Registro de servicios para Kafka y TCP
builder.Services.AddSingleton<IKafkaProducer, KafkaProducer>();

builder.Services.AddSingleton<IBdService, BdService>();

builder.Services.AddHostedService<TcpServer>(); // Registra TcpServer como servicio en segundo plano
builder.Services.AddHostedService<KafkaConsumer>(); // Registra KafkaConsumer como servicio en segundo plano

// Configuración de controladores y Swagger
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Configuración de Swagger para desarrollo
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

// Inicia la aplicación
app.Run();
