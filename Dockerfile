FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY ["StockQuoteAlert.sln", "./"]
COPY ["src/StockQuoteAlert/StockQuoteAlert.csproj", "src/StockQuoteAlert/"]
COPY ["tests/StockQuoteAlert.Tests/StockQuoteAlert.Tests.csproj", "tests/StockQuoteAlert.Tests/"]

RUN dotnet restore "StockQuoteAlert.sln"

COPY . .

WORKDIR "/src/src/StockQuoteAlert"

RUN dotnet publish "StockQuoteAlert.csproj" -c Release -o /app/publish /p:UseAppHost=false

RUN cp appsettings.json /app/publish/ || true

FROM mcr.microsoft.com/dotnet/runtime:8.0 AS final
WORKDIR /app

RUN adduser --disabled-password --gecos "" appuser && chown -R appuser /app
USER appuser

COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "StockQuoteAlert.dll"]
