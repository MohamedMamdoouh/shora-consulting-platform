FROM node:22-alpine AS frontend
WORKDIR /src/frontend
COPY src/frontend/package.json src/frontend/package-lock.json ./
RUN npm ci
COPY src/frontend/ ./
COPY src/contracts/ ../contracts/
RUN npm run build

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS backend
WORKDIR /src/backend
COPY src/backend/ ./
COPY --from=frontend /src/frontend/dist/shora-web/browser/ ./Shora.Api/wwwroot/
RUN dotnet publish Shora.Api/Shora.Api.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
RUN apt-get update \
    && apt-get install -y --no-install-recommends libgssapi-krb5-2 \
    && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=backend /app/publish .
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
CMD ["dotnet", "Shora.Api.dll"]
