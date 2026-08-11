# Runtime image for Railway (and local smoke tests).
# CI builds the app first and places output in ./publish (see .github/workflows/deploy.yml).
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY publish/ .
EXPOSE 8080
CMD ASPNETCORE_URLS="http://+:${PORT:-8080}" dotnet Shora.Api.dll
