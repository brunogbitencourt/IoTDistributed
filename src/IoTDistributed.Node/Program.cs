
using IoTDistributed.Node.Services;
using IoTDistributed.Node.Workers;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Npgsql;
using IoTDistributed.Node.Persistence;

var builder = WebApplication.CreateBuilder(args);

int port = builder.Configuration.GetValue<int>(
    "GRPC_PORT", 8080);

builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenAnyIP(port, listen =>
    {
        listen.Protocols = HttpProtocols.Http2;
    });
});

builder.Services.AddGrpc();

string connectionString =
    builder.Configuration.GetConnectionString("Default")
    ?? throw new InvalidOperationException(
        "Connection string não configurada.");

builder.Services.AddSingleton(
    _ => NpgsqlDataSource.Create(connectionString));


builder.Services.AddHostedService<DatabaseConnectionWorker>();
builder.Services.AddScoped<SensorRepository>();
// builder.Services.AddHostedService<SensorReadingTestWorker>();
builder.Services.AddScoped<ReplicationService>();

var app = builder.Build();

app.MapGrpcService<ClusterGrpcService>();

app.Run();
