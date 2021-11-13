
# The GitHub CI/CD guide for .NET 5/6
- #1. Creating a Containerized NET App
- #2. The Build & Publish Pipeline|2. The Build & Publish Pipeline
- #3. Running Tests as part of the Pipeline|3. Running Tests as part of the Pipeline
- #4. Splitting up Pipelines|4. Splitting up Pipelines

---
## 1. Creating a Containerized .NET App
First we need to containerize our application to make sure we have the same reproducible building steps. This prevents the classic "It builds and works on my machine". This way it doesn't matter which machine is building the image. It's always build the same way.

### (Option 1): Using Visual Studio / Rider
1. First open Visual Studio or Rider.
2. Choose the template `ASP.NET Core Wep Application`.
3. Give it a name.
4. Enable versioning by Selecting `Create Git repository`.
5. Choose the type `Web API`
6. Enable `Docker Support`
	1. Choose Linux if using Linux containers i.e. WSL or Hyper-V (Recommended)
	2. Choose Windows if using Windows containers i.e. Hyper-V
7. You should now have something matching the picture below:
![[Pasted image 20211113114745.png]]
8. Press `Create`.


### (Option 2): Using the dotnet CLI
Create the solution folder:
```shell:
mkdir ci-cd-lecture
```
Change directory to the solution folder:
```shell:
cd ci-cd-lecture
```

Create the project inside the solution folder:
```shell:
dotnet new webapi -o MyWebApi
```

Create a `Dockerfile` in the project directory with the contents from [[#The Docker file]] section.

### The Dockerfile
You should now have the following Dockerfile in your project directory:
```dockerfile:MyWebApi/Dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:5.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:5.0 AS build
WORKDIR /src
COPY ["MyApi/MyApi.csproj", "MyApi/"]
RUN dotnet restore "MyApi/MyApi.csproj"
COPY . .
WORKDIR "/src/MyApi"
RUN dotnet build "MyApi.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "MyApi.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "MyApi.dll"]
```

#### Base
```dockerfile:MyWebApi/Dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:5.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

...
```

This is the base of which our image is build upon.

#### Build
```dockerfile:MyWebApi/Dockerfile
...

FROM mcr.microsoft.com/dotnet/sdk:5.0 AS build
WORKDIR /src
COPY ["MyApi/MyApi.csproj", "MyApi/"]
RUN dotnet restore "MyApi/MyApi.csproj"
COPY . .
WORKDIR "/src/MyApi"
RUN dotnet build "MyApi.csproj" -c Release -o /app/build

...
```

Here we first copy the `MyWebApi.csproj` project file and then restore our NuGet packages.  
We then copy the entire solution to our image and then builds the Release version of our application.

#### Publish
```dockerfile:MyWebApi/Dockerfile
...

FROM build AS publish
RUN dotnet publish "MyApi.csproj" -c Release -o /app/publish

...
```

Here we make dotnet publish our application which builds and optimizes our code and artifacts ready to release.

#### Final
```dockerfile:MyWebApi/Dockerfile
...

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "MyApi.dll"]
```

Finally we copy the app from where we published our application in the `publish` step.  
This is crucial for having a well optimized and small image to deploy later on. Because we avoid copying all the other cached files or source code that would otherwise just bloat our image for no reason.

---
## 2. The Build & Publish Pipeline
We are going to use GitHub Actions in this example for simplicity and easy access for everyone, but the general concepts apply to all CI/CD pipeline tools.

### Building the Image
```yaml:.github/workflows/build-pipeline.yml
name: Image Build Pipeline

on:
  push:
    branches: [ main ]
  pull_request:
    branches: [ main ]

jobs:

  build:
    runs-on: ubuntu-latest

    steps:
    - uses: actions/checkout@v2
    - name: Build the Docker image
      run: docker build . --file Dockerfile --tag my-web-api:$(date +%s)
```

#### Test it out!
Add a new commit and push it to the main branch and see if it executes.

### Extending the Pipeline with Publishing
```yaml:.github/workflows/build-pipeline.yml
name: Docker

on:
  push:
    branches: [ main ]
    tags: [ 'v*.*.*' ]
  pull_request:
    branches: [ main ]

env:
  REGISTRY: ghcr.io
  IMAGE_NAME: ${{ github.repository }}


jobs:
  build:

    runs-on: ubuntu-latest
    permissions:
      contents: read
      packages: write

    steps:
      - name: Checkout repository
        uses: actions/checkout@v2

      - name: Log into registry ${{ env.REGISTRY }}
        if: github.event_name != 'pull_request'
        uses: docker/login-action@28218f9b04b4f3f62068d7b6ce6ca5b26e35336c
        with:
          registry: ${{ env.REGISTRY }}
          username: ${{ github.actor }}
          password: ${{ secrets.GITHUB_TOKEN }}

      - name: Extract Docker metadata
        id: meta
        uses: docker/metadata-action@98669ae865ea3cffbcbaa878cf57c20bbf1c6c38
        with:
          images: ${{ env.REGISTRY }}/${{ env.IMAGE_NAME }}

      - name: Build and push Docker image
        uses: docker/build-push-action@ad44023a93711e3deb337508980b4b5e9bcdc5dc
        with:
          context: .
          push: ${{ github.event_name != 'pull_request' }}
          tags: ${{ steps.meta.outputs.tags }}
          labels: ${{ steps.meta.outputs.labels }}
```

#### Test it out!

##### (Option 1): Using Visual Studio / Rider

##### (Option 2): Using the dotnet CLI

---
## 3. Running Tests as part of the Pipeline
All this is cool, but we need to make sure our tests pass, before we publish anything.

### (Option 1): Within the Dockerfile

### (Option 2): Within the Pipeline

---
## 4. Splitting up Pipelines

### Separate Build and Tests

### Adding Manual Execution of a Pipeline
