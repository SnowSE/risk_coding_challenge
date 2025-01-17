#See https://aka.ms/containerfastmode to understand how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
RUN apt-get update && apt-get install -y curl
WORKDIR /app
EXPOSE 80

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY ["Risk.Signalr.CS1400Client/Risk.Signalr.CS1400Client.csproj", "Risk.Signalr.CS1400Client/"]
COPY ["Risk.Shared/Risk.Shared.csproj", "Risk.Shared/"]
COPY ["Risk.Akka/Risk.Akka.csproj", "Risk.Akka/"]
RUN dotnet restore "Risk.Signalr.CS1400Client/Risk.Signalr.CS1400Client.csproj"
COPY . .
WORKDIR "/src/Risk.Signalr.CS1400Client"
RUN dotnet build "Risk.Signalr.CS1400Client.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "Risk.Signalr.CS1400Client.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Risk.Signalr.CS1400Client.dll"]