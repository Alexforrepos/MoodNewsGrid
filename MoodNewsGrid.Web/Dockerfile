FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY MoodNewsGrid.Core/*.csproj MoodNewsGrid.Core/
COPY MoodNewsGrid.Infrastructure/*.csproj MoodNewsGrid.Infrastructure/
COPY MoodNewsGrid.Web/*.csproj MoodNewsGrid.Web/
RUN dotnet restore MoodNewsGrid.Web/MoodNewsGrid.Web.csproj

COPY MoodNewsGrid.Core/ MoodNewsGrid.Core/
COPY MoodNewsGrid.Infrastructure/ MoodNewsGrid.Infrastructure/
COPY MoodNewsGrid.Web/ MoodNewsGrid.Web/

RUN dotnet publish MoodNewsGrid.Web/MoodNewsGrid.Web.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app .

EXPOSE 8080
ENTRYPOINT ["dotnet", "MoodNewsGrid.Web.dll"]
