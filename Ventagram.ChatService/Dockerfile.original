FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["Ventagram.ChatService/Ventagram.ChatService.csproj", "Ventagram.ChatService/"]
RUN dotnet restore "Ventagram.ChatService/Ventagram.ChatService.csproj"
COPY . .
WORKDIR "/src/Ventagram.ChatService"
RUN dotnet publish "Ventagram.ChatService.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "Ventagram.ChatService.dll"]
