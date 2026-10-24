
# ETAPA 1 - BUILD
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build

WORKDIR /src

# Copia o arquivo do projeto
COPY src/IoTDistributed.Node/IoTDistributed.Node.csproj src/IoTDistributed.Node/

# Restaura as dependências
RUN dotnet restore src/IoTDistributed.Node/IoTDistributed.Node.csproj

# Copia o restante do código
COPY . .

# Compila e publica a aplicação
RUN dotnet publish src/IoTDistributed.Node/IoTDistributed.Node.csproj -c Release -o /app/publish --no-restore

# ETAPA 2 - RUNTIME
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime

WORKDIR /app

# Copia os arquivos publicados
COPY --from=build /app/publish .

# Inicia a aplicação
ENTRYPOINT ["dotnet", "IoTDistributed.Node.dll"]
