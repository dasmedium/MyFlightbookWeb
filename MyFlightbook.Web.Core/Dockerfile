FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["MyFlightbook.Web.Core/MyFlightbook.Web.Core.csproj", "MyFlightbook.Web.Core/"]
COPY ["MyFlightbookData/MyFlightbook.Data.csproj", "MyFlightbookData/"]
RUN dotnet restore "MyFlightbook.Web.Core/MyFlightbook.Web.Core.csproj"

COPY . .
WORKDIR "/src/MyFlightbook.Web.Core"
RUN dotnet publish "MyFlightbook.Web.Core.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/* \
    && mkdir -p /app/images \
    && chown -R app:app /app

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

COPY --from=build /app/publish .

USER app

HEALTHCHECK --interval=10s --timeout=3s --retries=5 \
    CMD curl -fsS http://localhost:8080/healthz || exit 1

ENTRYPOINT ["dotnet", "MyFlightbook.Web.Core.dll"]
