FROM mcr.microsoft.com/dotnet/aspnet:5.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:5.0 AS build
COPY ["MyApi/MyApi.csproj", "MyApi/"]
COPY ["MyApiTest/MyApiTest.csproj", "MyApiTest/"]
RUN dotnet restore "MyApi/MyApi.csproj"
RUN dotnet restore "MyApiTest/MyApiTest.csproj"
COPY . .
RUN dotnet build "MyApi/MyApi.csproj" -c Release -o /app/build

FROM mcr.microsoft.com/dotnet/sdk:5.0 AS test
COPY --from=build . .
RUN dotnet test "MyApiTest/MyApiTest.csproj"

FROM build AS publish
RUN dotnet publish "MyApi/MyApi.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "MyApi.dll"]
