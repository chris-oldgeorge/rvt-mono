#!/usr/bin/env bash
set -euo pipefail

dotnet restore RvtPortal.Spa.sln --nologo --disable-build-servers
dotnet build RvtPortal.Spa.sln --configuration Release --no-restore --nologo --disable-build-servers
dotnet test RvtPortal.Spa.Tests/RvtPortal.Spa.Tests.csproj --configuration Release --no-build --nologo --disable-build-servers
dotnet publish RvtPortal.Spa/RvtPortal.Spa.csproj --configuration Release --no-build --output artifacts/backend --nologo --disable-build-servers
test -f artifacts/backend/RvtPortal.Spa.dll
