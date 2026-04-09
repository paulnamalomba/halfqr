FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY HaveQR.sln ./
COPY microservices/HaveQR.Contracts/HaveQR.Contracts.csproj microservices/HaveQR.Contracts/
COPY microservices/HaveQR.QrEngine/HaveQR.QrEngine.csproj microservices/HaveQR.QrEngine/
COPY microservices/HaveQR.PublicApi/HaveQR.PublicApi.csproj microservices/HaveQR.PublicApi/

RUN dotnet restore microservices/HaveQR.PublicApi/HaveQR.PublicApi.csproj

COPY microservices/ ./microservices/

RUN dotnet publish microservices/HaveQR.PublicApi/HaveQR.PublicApi.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

RUN apt-get update \
    && apt-get install -y --no-install-recommends libfontconfig1 libfreetype6 libpng16-16 \
    && rm -rf /var/lib/apt/lists/*

ENV ASPNETCORE_URLS=http://0.0.0.0:8080

COPY --from=build /app/publish ./

EXPOSE 8080

ENTRYPOINT ["dotnet", "HaveQR.PublicApi.dll"]
