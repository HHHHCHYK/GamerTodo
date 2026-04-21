FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["Directory.Build.props", "./"]
COPY ["HeyeTodo.sln", "./"]
COPY ["src/HeyeTodo.Shared/HeyeTodo.Shared.csproj", "src/HeyeTodo.Shared/"]
COPY ["src/HeyeTodo.Server/HeyeTodo.Server.csproj", "src/HeyeTodo.Server/"]

RUN dotnet restore "src/HeyeTodo.Server/HeyeTodo.Server.csproj"

COPY . .
WORKDIR /src/src/HeyeTodo.Server
RUN dotnet publish "HeyeTodo.Server.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

EXPOSE 8080
ENTRYPOINT ["dotnet", "HeyeTodo.Server.dll"]
