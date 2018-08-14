Tmds.Varlink is a library to consume [varlink](https://varlink.org) services from .NET.

Tmds.Varlink.Tool is a dotnet CLI tool to generate C# code based on varlink interface descriptions.

# Example

In this example we'll use the podman varlink interface (https://www.projectatomic.io/blog/2018/05/podman-varlink/).

The following steps need to be performed as `root` since the podman service is not accessible by regular users.

Before we can start coding, we enable the podman socket:

```
# systemctl enable io.projectatomic.podman.socket
```

The podman varlink service can now be accessed at `/run/podman/io.projectatomic.podman`.

Create a new console project:

```
# dotnet new console -o PodmanExample
# cd PodmanExample
```

Add a `NuGet.Config` file to get the NuGet packages from myget:

```xml
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <add key="tmds" value="https://www.myget.org/F/tmds/api/v3/index.json" />
  </packageSources>
</configuration>
```

Next, we add a reference to the packages in the `Podman.csproj` file and update the `LangVersion`.

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>netcoreapp2.1</TargetFramework>
    <LangVersion>latest</LangVersion>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Tmds.Varlink" Version="0.1.0-*" />
    <DotNetCliToolReference Include="Tmds.Varlink.Tool" Version="0.1.0-*" />
  </ItemGroup>
</Project>
```

Now run `dotnet restore` to fetch the tool.

```
# dotnet restore
```

Now we use the tool to generate a proxy for the podman service:

```
# dotnet varlink unix:/run/podman/io.projectatomic.podman
Written io.projectatomic.podman.cs
```

Edit `Program.cs` to use the podman service to list the images.

```cs
static async Task Main(string[] args)
{
    string address = "unix:/run/podman/io.projectatomic.podman";

    ListImagesResult result;
    using (var podman = new Podman(address))
    {
        result = await podman.ListImagesAsync();
    }

    PrintImages(result);
}

private static void PrintImages(ListImagesResult result)
{
    Console.WriteLine("Images:");
    foreach (var image in result.images)
    {
        if (image.repoTags != null)
        {
            Console.WriteLine($"* {string.Join(", ", image.repoTags)} ({image.id})");
        }
        else
        {
            Console.WriteLine($"* {image.id}");
        }
    }
}
```

Let's run the example:
```
# dotnet run
Images:
* docker.io/library/centos:7 (49f7960eb7e4cb46f1a02c1f8174c6fac07ebf1eb6d8deffbcb5c695f1c9edd5)
* docker.io/library/busybox:latest (e1ddd7948a1c31709a23cc5b7dfe96e55fc364f90e1cebcde0773a1b5a30dcda)
```
