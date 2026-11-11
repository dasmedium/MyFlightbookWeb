#!/usr/bin/env bash
set -euo pipefail

cd /src

echo "Running MyFlightbook.Telemetry.Tests"
dotnet test MyFlightbook.Telemetry.Tests/MyFlightbook.Telemetry.Tests.csproj --nologo

echo "Running MyFlightbook.FlightDeckScan.Tests"
dotnet test MyFlightbook.FlightDeckScan.Tests/MyFlightbook.FlightDeckScan.Tests.csproj --nologo

echo "Running MyFlightbook.Data.IntegrationTests"
dotnet test MyFlightbook.Data.IntegrationTests/MyFlightbook.Data.IntegrationTests.csproj --nologo
