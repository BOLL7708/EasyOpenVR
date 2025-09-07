# EasyOpenVR
This is a Class Library to make it easier to talk to the OpenVR API using the official C# headers.

## Disclaimer
This is a collection of methods and functions that has been figured out while trying to interface OpenVR through pure C#, not via a game engine. The project was made to enable easy access to these calls without the repeated struggle of raw implementation. 

As this is a work-in-progress there might be breaking changes along the way, the hope is to keep that to a minimum, as this is used in multiple projects. If nothing else, the code in this project can act as a place to reference how to call certain OpenVR APIs from C#.

## Installation
1. To use this either download the repo directly or add it as a git submodule by running the following command **in the root of your project**, optionally replace `TargetFolder` with your own value: `git submodule add https://github.com/BOLL7708/EasyOpenVR.git EasyOpenVR`
2. In the `EasyOpenVR` folder, run the `.cmd` file which downloads the latest OpenVR dependencies into the project.
3. This is a Class Library, so to use this you add an `Existing Project` to your solution, pick the `EasyOpenVR` folder or depending on IDE the solutions file in the folder, and it should show up next to your current project in your solution.
4. Then reference this class library it in the main project .csproj file, in `<ItemGroup>`, or let your IDE add it when referenced, if it can do that.
```xml
<ProjectReference Include="..\EasyOpenVR\EasyOpenVR.csproj" />
```
5. Make sure to build for 64bit.

## Usage
1. To use the singleton class, which is my current approach, simply include the namespace by adding `using BOLL7708;` and then grab an instance from `EasyOpenVRSingleton.Instance`
2. If you want your application to be something else than `VRApplication_Background` you can set that with `instance.SetApplicationType()`
3. With the instance run `instance.Init()` to connect to a running OpenVR session. It will return true if initialization was successful, otherwise run a timer and try to init as often as you see fit.
4. At this point, if you have a connected session, you can explore the various calls you can do.
