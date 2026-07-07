FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY HalfQR.sln ./
COPY microservices/HalfQR.Contracts/HalfQR.Contracts.csproj microservices/HalfQR.Contracts/
COPY microservices/HalfQR.QrEngine/HalfQR.QrEngine.csproj microservices/HalfQR.QrEngine/
COPY microservices/HalfQR.Worker/HalfQR.Worker.csproj microservices/HalfQR.Worker/

RUN dotnet restore microservices/HalfQR.Worker/HalfQR.Worker.csproj

COPY microservices/ ./microservices/

RUN dotnet publish microservices/HalfQR.Worker/HalfQR.Worker.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/runtime:10.0 AS runtime
WORKDIR /app

RUN apt-get update \
    && apt-get install -y --no-install-recommends libfontconfig1 libfreetype6 libpng16-16 \
    && rm -rf /var/lib/apt/lists/*

COPY --from=build /app/publish ./

ENTRYPOINT ["dotnet", "HalfQR.Worker.dll"]
