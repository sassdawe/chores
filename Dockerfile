FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY Chores/Chores.csproj Chores/
RUN dotnet restore Chores/Chores.csproj

COPY Chores/ Chores/
RUN dotnet publish Chores/Chores.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# SQLite data lives here — mount a volume to persist it
VOLUME /data
ENV DataDirectory=/data

COPY --from=build /app/publish .

EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080

ENTRYPOINT ["dotnet", "Chores.dll"]
