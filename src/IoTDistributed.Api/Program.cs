
using IoTDistributed.Api.Persistence;
using Npgsql;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddOpenApi();

// Obtém a conexão configurada nos User Secrets.
string connectionString =
    builder.Configuration.GetConnectionString("Default")
    ?? throw new InvalidOperationException(
        "ConnectionStrings:Default não configurada.");

// Registra o NpgsqlDataSource como singleton.
builder.Services.AddSingleton(
    _ => NpgsqlDataSource.Create(connectionString));

// Registra repositório.
builder.Services.AddScoped<SensorRepository>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseAuthorization();

app.MapControllers();

app.Run();
