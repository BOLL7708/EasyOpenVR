# EasyOpenVR
This is a git submodule to make it easier to talk to the OpenVR API using the C# headers.

## Disclaimer
This is a project where I have collected things I've figured out while trying to interface OpenVR through C#. I'm not a C# programmer by trade so this is closer to a "get something done at all"-project than something hyper optimized. 

As this is a work-in-progress things might get renamed along the way, hopefully I can keep that to a minimum as I do use this myself, in multiple projects. If nothing else this can act as a place to reference how to call certain OpenVR API functions from C#, some of them took me some time to figure out.

## Installation
1. To use this either download the repo/file directly or add it as a git submodule by running the following command in the root of your project, replace `TargetFolder` with your own value: `git submodule add https://github.com/BOLL7708/EasyOpenVR.git TargetFolder/EasyOpenVR`
2. Download these dependencies from [OpenVR](https://github.com/ValveSoftware/openvr), the files are [openvr_api.cs](https://github.com/ValveSoftware/openvr/blob/master/headers/openvr_api.cs) and [openvr_api.dll](https://github.com/ValveSoftware/openvr/blob/master/bin/win64/openvr_api.dll), put them in the root of your project. 
3. Include the files in your project and set the `.dll` to `Copy always` and build your project for 64bit.

## Usage
1. To use the singleton class, which is my current approach, simply include the namespace by adding `using BOLL7708;` and then grab an instance from `EasyOpenVRSingleton.Instance`
2. If you want your application to be something else than `VRApplication_Background` you can set that with `instance.SetApplicationType()`
3. With the instance run `instance.Init()` to connect to a running OpenVR session. It will return true if initialization was successful, otherwise run a timer and try to init as often as you see fit.
4. At this point, if you have a connected session, you can explore the various calls you can do.
5. If your project is missing for example `System.Drawing`, check References in your project, choose "_Add reference..._" and check `System.Drawing` for inclusion.
