FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["TCP-AQUTEST.csproj", "./"]
RUN dotnet restore
COPY . .
RUN dotnet build -c Release -o /app/build
RUN dotnet publish -c Release -o /app/publish

# Configura la zona horaria
RUN apt-get update && \
    apt-get install -y tzdata && \
    ln -fs /usr/share/zoneinfo/America/Bogota /etc/localtime && \
    dpkg-reconfigure -f noninteractive tzdata

FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app/publish .
ARG TCP_PORT
ENV TCP__Port=${TCP_PORT}
ENV TZ=America/Bogota

EXPOSE ${TCP_PORT}
ENTRYPOINT ["dotnet", "TCP-AQUTEST.dll"]