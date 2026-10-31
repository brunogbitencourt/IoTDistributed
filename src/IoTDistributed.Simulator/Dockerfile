FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY src/IoTDistributed.Simulator/IoTDistributed.Simulator.csproj src/IoTDistributed.Simulator/
RUN dotnet restore src/IoTDistributed.Simulator/IoTDistributed.Simulator.csproj

COPY . .
RUN dotnet publish src/IoTDistributed.Simulator/IoTDistributed.Simulator.csproj \
    -c Release --no-restore -o /app/publish

FROM mcr.microsoft.com/dotnet/runtime:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "IoTDistributed.Simulator.dll"]
