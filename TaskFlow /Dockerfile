FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY TaskFlow.sln ./
COPY src/TaskFlow.Api/TaskFlow.Api.csproj src/TaskFlow.Api/
COPY tests/TaskFlow.Tests/TaskFlow.Tests.csproj tests/TaskFlow.Tests/
RUN dotnet restore
COPY . .
RUN dotnet publish src/TaskFlow.Api -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:8080
RUN mkdir -p /data && chown app /data
COPY --from=build /app/publish .
USER app
EXPOSE 8080
ENTRYPOINT ["dotnet", "TaskFlow.Api.dll"]
