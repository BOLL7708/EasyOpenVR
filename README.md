# EasyOpenVR
This is a git submodule to make it easier to talk to the OpenVR API using the C# headers.

## Disclaimer
I'm making this as to collect all the things I've figured out and used in a plethora of private OpenVR projects. I'm not a C# programmer by trade so I expect many of the things I've implemented to have better solutions, naturally, this is more of a "get something done at all" project. 

As this is a work-in-progress things might get renamed along the way, but I am implementing this in my own projects as hopefully I can keep that to a minimum. If nothing else, this can be a place to reference how to call certain OpenVR API functions from C#, they were not all obvious to me.

## Usage
To use this either download the repo directly or add it as a git submodule by running this in your project git root, replace TargetFolder with your own value:
`git submodule add https://github.com/BOLL7708/EasyOpenVR.git TargetFolder/EasyOpenVR`

About the only dependency are files from [OpenVR](https://github.com/ValveSoftware/openvr), download [openvr_api.cs](https://github.com/ValveSoftware/openvr/blob/master/headers/openvr_api.cs) and [openvr_api.dll](https://github.com/ValveSoftware/openvr/blob/master/bin/win64/openvr_api.dll) and put them in your project root. Set the .dll to _Copy always_ and build for 64bit.
