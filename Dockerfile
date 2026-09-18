# Use the official .NET SDK image to build the app
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

RUN dotnet tool install --global dotnet-ef
ENV PATH="$PATH:/root/.dotnet/tools"
# Copy and restore as distinct layers
COPY *.csproj ./
RUN dotnet restore

# Copy everything else and build
COPY . ./
RUN dotnet publish -c Release -o /app/publish

# --- Runtime Stage ---
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app/publish .

# ---- ADD THIS LINE ----
# Copy the Uploads folder from your local source into the container
COPY Uploads/ ./Uploads/

# Expose port and set entry point
EXPOSE 80
ENTRYPOINT ["dotnet", "BizfreeApp.dll"]
