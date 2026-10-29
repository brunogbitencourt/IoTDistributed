
using IoTDistributed.Api.Database;
using IoTDistributed.Api.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddOpenApi();

// Gerencia as conexões com DB1, DB2 e DB3.
builder.Services.AddSingleton<ClusterDataSources>();

// Repositórios
builder.Services.AddScoped<SensorRepository>();
builder.Services.AddScoped<ReadingRepository>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseAuthorization();

app.MapControllers();

app.Run();
