FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY HaveQR.sln ./
COPY microservices/HaveQR.Contracts/HaveQR.Contracts.csproj microservices/HaveQR.Contracts/
COPY microservices/HaveQR.QrEngine/HaveQR.QrEngine.csproj microservices/HaveQR.QrEngine/
COPY microservices/HaveQR.Worker/HaveQR.Worker.csproj microservices/HaveQR.Worker/

RUN dotnet restore microservices/HaveQR.Worker/HaveQR.Worker.csproj

COPY microservices/ ./microservices/

RUN dotnet publish microservices/HaveQR.Worker/HaveQR.Worker.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/runtime:10.0 AS runtime
WORKDIR /app

RUN apt-get update \
    && apt-get install -y --no-install-recommends libfontconfig1 libfreetype6 libpng16-16 \
    && rm -rf /var/lib/apt/lists/*

COPY --from=build /app/publish ./

ENTRYPOINT ["dotnet", "HaveQR.Worker.dll"]
