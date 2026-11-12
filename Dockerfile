FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY src/ReleaseReadiness.Api/ReleaseReadiness.Api.csproj src/ReleaseReadiness.Api/
RUN dotnet restore src/ReleaseReadiness.Api/ReleaseReadiness.Api.csproj
COPY src/ src/
RUN dotnet publish src/ReleaseReadiness.Api/ReleaseReadiness.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app .
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
USER app
ENTRYPOINT ["dotnet", "ReleaseReadiness.Api.dll"]
