FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY global.json Directory.Build.props Directory.Packages.props MultiTenantSaaS.sln ./
COPY src/SaaS.Domain/SaaS.Domain.csproj src/SaaS.Domain/
COPY src/SaaS.Application/SaaS.Application.csproj src/SaaS.Application/
COPY src/SaaS.Infrastructure/SaaS.Infrastructure.csproj src/SaaS.Infrastructure/
COPY src/SaaS.API/SaaS.API.csproj src/SaaS.API/

RUN dotnet restore src/SaaS.API/SaaS.API.csproj

COPY src/ src/
RUN dotnet publish src/SaaS.API/SaaS.API.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app

RUN adduser --disabled-password --gecos "" appuser
USER appuser

COPY --from=build /app/publish .

EXPOSE 8080
ENV ASPNETCORE_HTTP_PORTS=8080

ENTRYPOINT ["dotnet", "SaaS.API.dll"]
