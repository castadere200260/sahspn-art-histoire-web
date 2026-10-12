# Build the ASP.NET Core MVC application.
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY sahspn-art-histoire.csproj ./
RUN dotnet restore ./sahspn-art-histoire.csproj

COPY . ./
RUN dotnet publish ./sahspn-art-histoire.csproj \
    --configuration Release \
    --output /app/publish \
    --no-restore \
    /p:UseAppHost=false

# Run on Render's HTTP port. TLS is terminated by Render's proxy.
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish ./

ENV ASPNETCORE_HTTP_PORTS=10000
ENV ASPNETCORE_FORWARDEDHEADERS_ENABLED=true
EXPOSE 10000

ENTRYPOINT ["dotnet", "sahspn-art-histoire.dll"]
