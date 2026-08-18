using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Valve.VR;

namespace EasyOpenVR;

public partial class EasyOpenVr
{
    public class SystemMethods(EasyOpenVr evr)
    {
        /**
         * Add an app manifest for the application
         * Pretty sure this is required to show up in the input bindings interface
         * OBS: Make sure the encoding is UTF8 and not UTF8+BOM
         */
        public EasyOpenVrResult AddAppManifest(string relativePath)
        {
            var error = OpenVR.Applications.AddApplicationManifest(Path.GetFullPath(relativePath), false);
            return evr.DebugLog(error);
        }

        public EasyOpenVrResult RemoveAppManifest(string relativePath)
        {
            var error = OpenVR.Applications.RemoveApplicationManifest(Path.GetFullPath(relativePath));
            return evr.DebugLog(error);
        }

        public EasyOpenVrResult SetAutoLaunch(string appKey, bool autoLaunch)
        {
            var installed = OpenVR.Applications.IsApplicationInstalled(appKey);
            if (!installed) return new EasyOpenVrResult(null, null, "To active autolaunch the application needs to be installed by registering an application manifest.");
            var error = OpenVR.Applications.SetApplicationAutoLaunch(appKey, autoLaunch);
            return evr.DebugLog(error);
        }

        /// <summary>
        /// Will add the application manifest and optionally register for auto launch.
        /// OBS: For auto launch to work the manifest must include "is_dashboard_overlay": true.
        /// OBS: Will only register for auto launch if not already installed.
        /// </summary>
        /// <param name="relativeManifestPath">The relative path to your application manifest</param>
        /// <param name="applicationKey">Application key, used to check if already installed.</param>
        /// <param name="alsoRegisterAutoLaunch">Optional flag to register for auto launch.</param>
        /// <returns></returns>
        public EasyOpenVrResult AddApplicationManifest(string relativeManifestPath, string applicationKey,
            bool alsoRegisterAutoLaunch = false)
        {
            if (OpenVR.Applications.IsApplicationInstalled(applicationKey)) return new EasyOpenVrResult(null, null, "Application was already installed.");
            var manifestError = OpenVR.Applications.AddApplicationManifest(Path.GetFullPath(relativeManifestPath), false);
            if (manifestError != EVRApplicationError.None || !alsoRegisterAutoLaunch) return evr.DebugLog(manifestError);
            var autolaunchError = OpenVR.Applications.SetApplicationAutoLaunch(applicationKey, true);
            return evr.DebugLog(autolaunchError);
        }

        /**
         * Will return the application ID for the currently running scene application.
         * Will return an empty string is there is no result.
         */
        public string GetRunningApplicationId()
        {
            var pid = OpenVR.Applications.GetCurrentSceneProcessId();
            if (pid == 0)
            {
                return string.Empty;
            }

            var sb = new StringBuilder((int)OpenVR.k_unMaxApplicationKeyLength);
            var error = OpenVR.Applications.GetApplicationKeyByProcessId(pid, sb, OpenVR.k_unMaxApplicationKeyLength);
            evr.DebugLog(error);
            return sb.ToString();
        }

        public string GetApplicationPropertyString(string applicationKey, EVRApplicationProperty applicationProperty)
        {
            if (string.IsNullOrEmpty(applicationKey))
            {
                return string.Empty;
            }

            var error = new EVRApplicationError();
            var sbLenght =
                (int)OpenVR.Applications.GetApplicationPropertyString(
                    applicationKey,
                    applicationProperty,
                    null,
                    0,
                    ref error
                );
            var sb = new StringBuilder(sbLenght);
            OpenVR.Applications.GetApplicationPropertyString(
                applicationKey,
                applicationProperty,
                sb,
                (uint)sbLenght,
                ref error
            );
            evr.DebugLog(error);
            return sb.ToString();
        }

        public string GetRuntimeVersion()
        {
            var version = "N/A";
            if (OpenVR.IsRuntimeInstalled())
            {
                version = OpenVR.System.GetRuntimeVersion();
            }

            return version;
        }

        /**
         * Listen for a VREvent_Quit and run this afterwards for your application to not get terminated. Then run Shutdown.
         */
        public void AcknowledgeShutdown()
        {
            OpenVR.System.AcknowledgeQuit_Exiting();
        }

        /**
         * Run this after AcknowledgeShutdown and after finishing all work, or OpenVR will likely throw an exception.
         */
        public void Shutdown()
        {
            OpenVR.Shutdown();
            evr._initState = 0;
            evr.Event.Handlers.Clear();
            evr.Input._inputActions = [];
        }
    }
}