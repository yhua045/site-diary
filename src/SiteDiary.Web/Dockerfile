FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY ["src/SiteDiary.Domain/SiteDiary.Domain.csproj", "src/SiteDiary.Domain/"]
COPY ["src/SiteDiary.Application/SiteDiary.Application.csproj", "src/SiteDiary.Application/"]
COPY ["src/SiteDiary.Infrastructure/SiteDiary.Infrastructure.csproj", "src/SiteDiary.Infrastructure/"]
COPY ["src/SiteDiary.Web/SiteDiary.Web.csproj", "src/SiteDiary.Web/"]

RUN dotnet restore "src/SiteDiary.Web/SiteDiary.Web.csproj"

COPY . .
WORKDIR /src/src/SiteDiary.Web
RUN dotnet publish "SiteDiary.Web.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app

ENV ASPNETCORE_URLS=http://+:8080

EXPOSE 8080

COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "SiteDiary.Web.dll"]