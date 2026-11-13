FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY src/ClubeDaOnca.Domain/ClubeDaOnca.Domain.csproj src/ClubeDaOnca.Domain/
COPY src/ClubeDaOnca.Contracts/ClubeDaOnca.Contracts.csproj src/ClubeDaOnca.Contracts/
COPY src/ClubeDaOnca.Api/ClubeDaOnca.Api.csproj src/ClubeDaOnca.Api/
RUN dotnet restore src/ClubeDaOnca.Api/ClubeDaOnca.Api.csproj
COPY src/ClubeDaOnca.Domain/ src/ClubeDaOnca.Domain/
COPY src/ClubeDaOnca.Contracts/ src/ClubeDaOnca.Contracts/
COPY src/ClubeDaOnca.Api/ src/ClubeDaOnca.Api/
RUN dotnet publish src/ClubeDaOnca.Api/ClubeDaOnca.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0
# LibreOffice (sem interface) converte o contrato de adesão (.docx) em PDF; fontes para acentuação correta.
RUN apt-get update     && apt-get install -y --no-install-recommends libreoffice-writer-nogui fonts-dejavu fonts-liberation     && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=build /app .
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "ClubeDaOnca.Api.dll"]
