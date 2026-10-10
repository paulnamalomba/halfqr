FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY HalfQR.sln ./
COPY microservices/HalfQR.Contracts/HalfQR.Contracts.csproj microservices/HalfQR.Contracts/
COPY microservices/HalfQR.Identity/HalfQR.Identity.csproj microservices/HalfQR.Identity/

RUN dotnet restore microservices/HalfQR.Identity/HalfQR.Identity.csproj

COPY microservices/ ./microservices/

RUN dotnet publish microservices/HalfQR.Identity/HalfQR.Identity.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

ENV ASPNETCORE_URLS=http://0.0.0.0:8080

COPY --from=build /app/publish ./

EXPOSE 8080

ENTRYPOINT ["dotnet", "HalfQR.Identity.dll"]
