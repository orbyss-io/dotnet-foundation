# syntax=docker/dockerfile:1@sha256:4edf897a3ffa55b89f906fc8cc78afdb3f1834cc9c7083565e611a8a7d5fe99e
FROM mcr.microsoft.com/dotnet/sdk:10.0.403@sha256:de4fe90a98845d75910b9769662465c84de1a934665c431279d1382b2bddc02b AS build
WORKDIR /src
COPY . .
RUN dotnet restore Orbyss.Foundation.slnx --locked-mode --configfile NuGet.config
RUN dotnet publish src/Orbyss.Foundation.Host/Orbyss.Foundation.Host.csproj -c Release --no-restore -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0@sha256:de0ee358cdd672f7017a003bed21e9ac9ed48a27c922a59b18175b6e82fa7fee AS final
# The Host does not use APT. Remove the non-Essential manager/library offline (CVE-2026-109518).
RUN dpkg --purge apt libapt-pkg6.0t64 \
    && ! dpkg-query --status apt 2>/dev/null \
    && ! dpkg-query --status libapt-pkg6.0t64 2>/dev/null \
    && test ! -e /usr/bin/apt \
    && test ! -e /usr/bin/apt-get
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
COPY --from=build /app/publish ./
RUN mkdir -p /app/packages \
    && chown -R $APP_UID:$APP_UID /app
USER $APP_UID
ENTRYPOINT ["dotnet", "Orbyss.Foundation.Host.dll"]
