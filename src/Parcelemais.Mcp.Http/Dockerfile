FROM mcr.microsoft.com/dotnet/sdk:8.0-alpine AS build
WORKDIR /src

COPY global.json Directory.Build.props ./
COPY src/Parcelemais.Mcp.Tools/Parcelemais.Mcp.Tools.csproj src/Parcelemais.Mcp.Tools/
COPY src/Parcelemais.Mcp.Http/Parcelemais.Mcp.Http.csproj src/Parcelemais.Mcp.Http/
RUN dotnet restore src/Parcelemais.Mcp.Http/Parcelemais.Mcp.Http.csproj

COPY src/ src/
RUN dotnet publish src/Parcelemais.Mcp.Http/Parcelemais.Mcp.Http.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0-alpine AS runtime
WORKDIR /app
COPY --from=build /app .

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "parcelemais-mcp-http.dll"]
