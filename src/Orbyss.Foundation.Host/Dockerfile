# syntax=docker/dockerfile:1@sha256:4edf897a3ffa55b89f906fc8cc78afdb3f1834cc9c7083565e611a8a7d5fe99e
FROM mcr.microsoft.com/dotnet/sdk:10.0.401@sha256:e70cdb7f80b0348f5cb85f19a8f670fca061f033d57eed12fa003d58b0e06317 AS build
WORKDIR /src
COPY . .
RUN dotnet restore Orbyss.Foundation.slnx --locked-mode --configfile NuGet.config
RUN dotnet publish src/Orbyss.Foundation.Host/Orbyss.Foundation.Host.csproj -c Release --no-restore -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0@sha256:222759b391a1aaf241166672c8f99b2d4ada452e7b5319f3c6e8f265a37b5ad4 AS final
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
COPY --from=build /app/publish ./
RUN mkdir -p /app/packages \
    && chown -R $APP_UID:$APP_UID /app
USER $APP_UID
ENTRYPOINT ["dotnet", "Orbyss.Foundation.Host.dll"]
