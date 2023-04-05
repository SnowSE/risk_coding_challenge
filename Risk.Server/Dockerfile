#See https://aka.ms/containerfastmode to understand how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/aspnet:6.0 AS base
WORKDIR /app
EXPOSE 80

FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build
WORKDIR /src
COPY ["Risk.Server/Risk.Server.csproj", "Risk.Server/"]
COPY ["Risk.Shared/Risk.Shared.csproj", "Risk.Shared/"]
COPY ["Risk.Akka/Risk.Akka.csproj", "Risk.Akka/"]
RUN dotnet restore "Risk.Server/Risk.Server.csproj"
COPY . .
WORKDIR "/src/Risk.Server"
RUN dotnet build "Risk.Server.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "Risk.Server.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Risk.Server.dll"]