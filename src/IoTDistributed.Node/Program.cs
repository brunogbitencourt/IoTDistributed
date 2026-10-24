
using IoTDistributed.Node.Services;
using IoTDistributed.Node.Workers;
using Microsoft.AspNetCore.Server.Kestrel.Core;

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
builder.Services.AddHostedService<PeerPingWorker>();

var app = builder.Build();

app.MapGrpcService<ClusterGrpcService>();

app.Run();
