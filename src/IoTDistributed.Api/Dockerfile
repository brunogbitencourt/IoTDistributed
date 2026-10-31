
# ETAPA 1 - Compilação
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build

WORKDIR /src

# Copia o arquivo de projeto
COPY src/IoTDistributed.Api/IoTDistributed.Api.csproj src/IoTDistributed.Api/

# Restaura dependências NuGet
RUN dotnet restore src/IoTDistributed.Api/IoTDistributed.Api.csproj

# Copia o código da API
COPY src/IoTDistributed.Api/ src/IoTDistributed.Api/

# Publica a aplicação
RUN dotnet publish src/IoTDistributed.Api/IoTDistributed.Api.csproj -c Release -o /app/publish --no-restore


# ETAPA 2 - Execução
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime

WORKDIR /app

ENV ASPNETCORE_URLS=http://+:8080

EXPOSE 8080

COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "IoTDistributed.Api.dll"]
