FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["TCP-AQUTEST.csproj", "./"]
RUN dotnet restore
COPY . .
RUN dotnet build -c Release -o /app/build
RUN dotnet publish -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app/publish .
ARG TCP_PORT
ENV TCP__Port=${TCP_PORT}
EXPOSE ${TCP_PORT}
ENTRYPOINT ["dotnet", "TCP-AQUTEST.dll"]