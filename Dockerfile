# syntax=docker/dockerfile:1

# ---------- build ----------
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Restore first so this layer is cached unless a .csproj changes.
COPY HotelManagement.sln .
COPY HotelManagement.API/HotelManagement.API.csproj                       HotelManagement.API/
COPY HotelManagement.Application/HotelManagement.Application.csproj        HotelManagement.Application/
COPY HotelManagement.Domain/HotelManagement.Domain.csproj                 HotelManagement.Domain/
COPY HotelManagement.Infrastructure/HotelManagement.Infrastructure.csproj HotelManagement.Infrastructure/
RUN dotnet restore HotelManagement.API/HotelManagement.API.csproj

# Build + publish.
COPY . .
RUN dotnet publish HotelManagement.API/HotelManagement.API.csproj -c Release -o /app --no-restore

# ---------- runtime ----------
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=build /app .

# Real values are injected by the host as environment variables:
#   ASPNETCORE_ENVIRONMENT, ConnectionStrings__DefaultConnection, Jwt__Secret,
#   Cors__Origins__0 ...  (see DEPLOYMENT notes)
ENV ASPNETCORE_ENVIRONMENT=Production
ENV PORT=8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "HotelManagement.API.dll"]
