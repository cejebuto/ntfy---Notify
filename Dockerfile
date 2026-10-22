FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY src/Notify.Web/Notify.Web.csproj src/Notify.Web/
RUN dotnet restore src/Notify.Web/Notify.Web.csproj
COPY src/ src/
RUN dotnet publish src/Notify.Web/Notify.Web.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:9.0
WORKDIR /app
COPY --from=build /app .
ENV ASPNETCORE_URLS=http://+:4444
EXPOSE 4444
USER app
ENTRYPOINT ["dotnet", "Notify.Web.dll"]
