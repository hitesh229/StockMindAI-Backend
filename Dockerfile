# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy project file and restore dependencies
COPY ["StockMindAI.API.csproj", "./"]
RUN dotnet restore "StockMindAI.API.csproj"

# Copy the rest of the source code
COPY . .

# Build the application
RUN dotnet build "StockMindAI.API.csproj" -c Release -o /app/build

# Publish stage
FROM build AS publish
RUN dotnet publish "StockMindAI.API.csproj" -c Release -o /app/publish

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=publish /app/publish .

# Expose port 5000 (HTTP) and 5001 (HTTPS)
EXPOSE 5000 5001

# Set environment to production
ENV ASPNETCORE_ENVIRONMENT=Production
ENV ASPNETCORE_URLS=http://+:5000

# Run the application
ENTRYPOINT ["dotnet", "StockMindAI.API.dll"]
