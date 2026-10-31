using IoTDistributed.Simulator.Workers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddHostedService<SensorSimulatorWorker>();

await builder.Build().RunAsync();
