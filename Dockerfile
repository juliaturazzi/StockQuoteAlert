FROM mcr.microsoft.com/dotnet/sdk:8.0 AS base
WORKDIR /src

COPY ["StockQuoteAlert.sln", "."]
COPY ["src/StockQuoteAlert/StockQuoteAlert.csproj", "src/StockQuoteAlert/"]
COPY ["tests/StockQuoteAlert.Tests/StockQuoteAlert.Tests.csproj", "tests/StockQuoteAlert.Tests/"]
RUN dotnet restore "StockQuoteAlert.sln"

COPY . .

FROM base AS test
WORKDIR /src
RUN dotnet test "tests/StockQuoteAlert.Tests/StockQuoteAlert.Tests.csproj" --no-restore

FROM base AS publish
WORKDIR "/src/src/StockQuoteAlert"

RUN dotnet publish "StockQuoteAlert.csproj" -c Release -o /app/publish /p:UseAppHost=false
RUN cp appsettings.json /app/publish/ || true

FROM mcr.microsoft.com/dotnet/runtime:8.0 AS final
WORKDIR /app

RUN adduser --disabled-password --gecos "" appuser && chown -R appuser /app
USER appuser

COPY --from=publish /app/publish .

ENTRYPOINT ["dotnet", "StockQuoteAlert.dll"]