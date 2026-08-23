FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build

WORKDIR /src

COPY ["SupermarketBot.csproj", "./"]

RUN dotnet restore "SupermarketBot.csproj"

COPY . .

RUN dotnet publish "SupermarketBot.csproj" \
    -c Release \
    -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final

WORKDIR /app

ENV ASPNETCORE_HTTP_PORTS=8080
ENV DOTNET_USE_POLLING_FILE_WATCHER=1

COPY --from=build /app/publish .

EXPOSE 8080

ENTRYPOINT ["dotnet", "SupermarketBot.dll"]