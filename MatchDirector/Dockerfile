ARG CONFIGURATION=Debug

FROM mcr.microsoft.com/dotnet/core/sdk:3.1 AS base
WORKDIR /app

FROM base AS build
WORKDIR /build

ARG CONFIGURATION

COPY MatchDirector.csproj .
RUN dotnet restore

COPY . .
RUN dotnet build -c ${CONFIGURATION} -o /app

FROM build AS publish
WORKDIR /build

ARG CONFIGURATION

RUN dotnet publish -c ${CONFIGURATION} -o /app

FROM base AS final
WORKDIR /app

COPY --from=publish /app .
COPY credential.json /app

ENV GOOGLE_APPLICATION_CREDENTIALS /app/credential.json

ENTRYPOINT ["dotnet", "MatchDirector.dll"]
