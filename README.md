# The GitHub CI/CD guide for .NET 5/6
1. [Creating a Containerized .NET App](#1--creating-a-Containerized--NET-App)
1. [[#2. The Build & Publish Pipeline|The Build & Publish Pipeline]]
1. [[#3. Running Tests as part of the Pipeline|Running Tests as part of the Pipeline]]

---
## 1. Creating a Containerized .NET App
First we need to containerize our application to make sure we have the same reproducible building steps. This prevents the classic "It builds and works on my machine". This way it doesn't matter which machine is building the image. It's always build the same way.

### (Option 1): Using Visual Studio / Rider
1. First open Visual Studio or Rider.
2. Choose the template 
	- Rider: `ASP.NET Core Wep Application`
	- Visual Studio: `ASP.NET Core Web API`.
	![1](https://user-images.githubusercontent.com/8335996/142277829-1a0f91d7-f9de-4275-aa32-6f6e375aabd6.png)
3. Give it a name.  
3.1. (Rider | Optional): Enable versioning by Selecting `Create Git repository`.  
3.2. (Rider): Choose the type `Web API`
7. Enable `Docker Support`
	1. Choose Linux if using Linux containers i.e. WSL or Hyper-V (Recommended)
	2. Choose Windows if using Windows containers i.e. Hyper-V
8. You should now have something matching a picture below:
- Rider ![Pasted image 20211113114745](https://user-images.githubusercontent.com/8335996/142277915-6d21a37f-8fe6-412f-8450-c669ce0c6797.png)
- Visual Studio ![3](https://user-images.githubusercontent.com/8335996/142277947-e54b9c8f-3770-422a-b8e1-d4dc1d7b8be2.png)
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


#### Test the build
From the solution folder, build the image:
```shell
docker build -t myapi -f MyAPI/Dockerfile .
```

Run the container:
```shell
 docker run --rm -it -e ASPNETCORE_ENVIRONMENT=Development -p 80:80 myapi
```

Go to the swagger UI to test it out:  
[http://127.0.0.1/swagger/index.html](http://127.0.0.1/swagger/index.html)

Press `Ctrl+C` in the terminal to stop the container.

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
      run: docker build . --file MyApi/Dockerfile --tag myapi:$(date +%s)
```

#### Test it out!
Add a new commit and push it to the main branch and see if it executes.

If you go to the `Actions` tab in the GitHub repository, you should see something like the following:
![Pasted image 20211117182211](https://user-images.githubusercontent.com/8335996/142278013-b70f3a6d-43ed-4b74-95fe-0df9cba3755f.png)

### Extending the Pipeline with Publishing
```yaml:.github/workflows/build-pipeline.yml
name: Image Build Pipeline

on:
  push:
    branches: [ main ]
    tags: [ 'v*.*.*' ]
  pull_request:
    branches: [ main ]

env:
  REGISTRY: ghcr.io
  IMAGE_NAME: myapi


jobs:
  build:

    runs-on: ubuntu-latest
    permissions:
      contents: read
      packages: write

    steps:
      - name: Checkout repository
        uses: actions/checkout@v2
		
	  - name: Build MyApi image
      	run: docker build . --file MyApi/Dockerfile --tag ${{ env.IMAGE_NAME }}:$(date +%s)
  
  publish:
    
	runs-on: ubuntu-latest
    permissions:
      contents: read
      packages: write
	
	steps:
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

---
## 3. Running Tests as part of the Pipeline
All this is cool, but we need to make sure our tests pass, before we publish anything.

### Create a Test project
Create a new test project in your solution and reference your API project.

In your `WeatherForecastController` add the following method:
```csharp
...

public bool ReturnTrue()
{	
	return true;
}

...
```

Add the NuGet package: `Moq`.

Then add the following unit test:
```csharp
using Microsoft.Extensions.Logging;
using Moq;
using MyApi.Controllers;
using Xunit;

namespace MyApiTest
{
    public class WeatherForecastControllerTest
    {
        [Fact]
        public void ShouldBe_ReturnTrue()
        {

            var logger = new Mock<ILogger<WeatherForecastController>>();
            var _sut = new WeatherForecastController(logger.Object);
            
            Assert.True(_sut.ReturnTrue());
        }
    }
}
```

### Run the tests in the pipeline
Add the test project to the build step in the Dockerfile:
```dockerfile
RUN dotnet restore "MyApiTest/MyApiTest.csproj"
```

So it should look like:
```dockerfile
...

FROM mcr.microsoft.com/dotnet/sdk:5.0 AS build  
COPY ["MyApi/MyApi.csproj", "MyApi/"]  
COPY ["MyApiTest/MyApiTest.csproj", "MyApiTest/"]  
RUN dotnet restore "MyApi/MyApi.csproj"  
RUN dotnet restore "MyApiTest/MyApiTest.csproj"  
COPY . .  
RUN dotnet build "MyApi/MyApi.csproj" -c Release -o /app/build

...
```

Add the following new step to the Dockerfile build:
```dockerfile
...

FROM mcr.microsoft.com/dotnet/sdk:5.0 AS test  
COPY --from=build . .  
RUN dotnet test "MyApiTest/MyApiTest.csproj"

...
```

#### Test it out!

Try making a commit and push it to the main branch.  
You should see a successful action execution like earlier.

Try to change the newly added controller method to return false instead of true:
```csharp
...

public bool ReturnTrue()
{	
	return false;
}

...
```

Make a new commit and push it to the main branch.  
You should now see it fail and if we look in the log you should see something familiar to the following image:
![failing-test](https://user-images.githubusercontent.com/8335996/142278041-4af6ca3d-4390-4dc8-ba19-5bfd2e7b5b09.png)
