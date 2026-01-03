# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy solution file
COPY ["attendenceProject.sln", "./"]

# Copy all project files
COPY ["attendenceProject/attendenceProject.csproj", "attendenceProject/"]
COPY ["attendence.Domain/attendence.Domain.csproj", "attendence.Domain/"]
COPY ["attendence.Data/attendence.Data.csproj", "attendence.Data/"]
COPY ["attendence.Services/attendence.Services.csproj", "attendence.Services/"]
COPY ["attendence.Tests/attendence.Tests.csproj", "attendence.Tests/"]

# Restore dependencies
RUN dotnet restore "attendenceProject/attendenceProject.csproj"

# Copy everything else
COPY . .

# Build
WORKDIR "/src/attendenceProject"
RUN dotnet build "attendenceProject.csproj" -c Release -o /app/build

# Publish stage
FROM build AS publish
RUN dotnet publish "attendenceProject.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=publish /app/publish .

EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

ENTRYPOINT ["dotnet", "attendenceProject.dll"]