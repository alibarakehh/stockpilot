FROM node:22-alpine AS web-build
WORKDIR /source/frontend
COPY frontend/package*.json ./
RUN npm ci
COPY frontend/ ./
RUN npm run build

FROM mcr.microsoft.com/dotnet/sdk:8.0-alpine AS api-build
WORKDIR /source
COPY Directory.Build.props .editorconfig ./
COPY backend/InventoryApi.csproj backend/
RUN dotnet restore backend/InventoryApi.csproj
COPY backend/ backend/
RUN dotnet publish backend/InventoryApi.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0-alpine AS final
WORKDIR /app
COPY --from=api-build /app/publish ./
COPY --from=web-build /source/frontend/dist ./wwwroot
USER $APP_UID
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "InventoryApi.dll"]
