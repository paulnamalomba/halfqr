FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY HalfQR.sln ./
COPY microservices/HalfQR.Contracts/HalfQR.Contracts.csproj microservices/HalfQR.Contracts/
COPY microservices/HalfQR.QrEngine/HalfQR.QrEngine.csproj microservices/HalfQR.QrEngine/
COPY microservices/HalfQR.PublicApi/HalfQR.PublicApi.csproj microservices/HalfQR.PublicApi/

RUN dotnet restore microservices/HalfQR.PublicApi/HalfQR.PublicApi.csproj

COPY microservices/ ./microservices/

RUN dotnet publish microservices/HalfQR.PublicApi/HalfQR.PublicApi.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

RUN apt-get update \
    && apt-get install -y --no-install-recommends libfontconfig1 libfreetype6 libpng16-16 \
    && rm -rf /var/lib/apt/lists/*

ENV ASPNETCORE_URLS=http://0.0.0.0:8080

COPY --from=build /app/publish ./

EXPOSE 8080

ENTRYPOINT ["dotnet", "HalfQR.PublicApi.dll"]
