FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY NetLab.sln ./
COPY src/NetLab.Api/NetLab.Api.csproj src/NetLab.Api/
COPY tests/NetLab.Tests/NetLab.Tests.csproj tests/NetLab.Tests/
RUN dotnet restore
COPY . .
RUN dotnet publish src/NetLab.Api -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:8080
COPY --from=build /app/publish .
USER app
EXPOSE 8080
ENTRYPOINT ["dotnet", "NetLab.Api.dll"]
