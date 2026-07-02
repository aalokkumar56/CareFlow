FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY *.sln ./
COPY src/CureFlow.Api/*.csproj ./src/CureFlow.Api/
COPY src/CureFlow.Application/*.csproj ./src/CureFlow.Application/
COPY src/CureFlow.Domain/*.csproj ./src/CureFlow.Domain/
COPY src/CureFlow.Infrastructure/*.csproj ./src/CureFlow.Infrastructure/
RUN dotnet restore src/CureFlow.Api/CureFlow.Api.csproj
COPY . .
WORKDIR /src/src/CureFlow.Api
RUN dotnet publish -c Release -o /app /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app ./
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "CureFlow.Api.dll"]
