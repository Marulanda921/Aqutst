using Microsoft.Extensions.Options;
using MongoDB.Driver;
using TCP_AQUTEST.Models.Kafka;
using System.Net;
using TCP_AQUTEST.Services.Implementations.Kafka;
using TCP_AQUTEST.Services.Implementations.Tpc;
using TCP_AQUTEST.Services.Contracts.DB;
using TCP_AQUTEST.Services.Contracts.Kafka;
using TCP_AQUTEST.Services.Implementations.DB;



//
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

builder.Services.AddSingleton<IDBService, DBService>();

// Registra TcpServer como servicio en segundo plano
builder.Services.AddHostedService<TcpService>();

// Registra KafkaConsumer como servicio en segundo plano
builder.Services.AddHostedService<KafkaConsumer>(); 

// Configuración de controladores y Swagger
builder.Services.AddEndpointsApiExplorer();

// Desactivar Kestrel
builder.WebHost.ConfigureKestrel(serverOptions =>
{
    // Puerto HTTP diferente, por ejemplo 5000
    serverOptions.ListenAnyIP(5000); 
});

var app = builder.Build();
// Inicia la aplicación
app.Run();
