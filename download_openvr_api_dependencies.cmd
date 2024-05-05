@echo off
echo ------------------------------------------------------------------
echo  Will download latest versions of OpenVR dependencies from Github
echo ------------------------------------------------------------------
echo.
echo Dowloading: openvr_api.cs
echo.
curl -L -o openvr_api.cs https://raw.githubusercontent.com/ValveSoftware/openvr/master/headers/openvr_api.cs
echo.
echo Downloading: openvr_api.dll
echo.
curl -L -o openvr_api.dll https://github.com/ValveSoftware/openvr/raw/master/bin/win64/openvr_api.dll
echo.
echo Downloading: openvr_api.dll.sig
echo.
curl -L -o openvr_api.dll.sig https://github.com/ValveSoftware/openvr/raw/master/bin/win64/openvr_api.dll.sig
echo.
echo Downloading: openvr_api.pdb
echo.
curl -L -o openvr_api.pdb https://github.com/ValveSoftware/openvr/raw/master/bin/win64/openvr_api.pdb
echo.
echo ------------------------------------------------------------------
echo.
echo Done!
echo.
pause