FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY . .
RUN dotnet restore Vairas.VehicleManagement/Vairas.VehicleManagement.csproj
RUN dotnet publish Vairas.VehicleManagement/Vairas.VehicleManagement.csproj -c Release --no-restore -o /app /p:UseAppHost=false
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .
ENV ASPNETCORE_HTTP_PORTS=8080
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "Vairas.VehicleManagement.dll"]
