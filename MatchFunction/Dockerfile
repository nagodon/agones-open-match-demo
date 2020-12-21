ARG CONFIGURATION=Debug

FROM mcr.microsoft.com/dotnet/core/aspnet:3.1 AS runtime
WORKDIR /app

FROM mcr.microsoft.com/dotnet/core/sdk:3.1 AS base
WORKDIR /app

FROM base AS build
WORKDIR /build

ARG CONFIGURATION

COPY MatchFunction.csproj .
RUN dotnet restore

COPY . .
RUN dotnet build -c ${CONFIGURATION} -o /app

FROM build AS publish
WORKDIR /build

ARG CONFIGURATION

RUN dotnet publish -c ${CONFIGURATION} -o /app

FROM runtime AS final
WORKDIR /app

COPY --from=publish /app .

ENTRYPOINT ["dotnet", "MatchFunction.dll"]
