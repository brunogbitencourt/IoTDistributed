
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build

WORKDIR /src

COPY src/IoTDistributed.Node/IoTDistributed.Node.csproj src/IoTDistributed.Node/

RUN dotnet restore src/IoTDistributed.Node/IoTDistributed.Node.csproj

COPY . .

RUN dotnet publish src/IoTDistributed.Node/IoTDistributed.Node.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime

WORKDIR /app

COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "IoTDistributed.Node.dll"]
