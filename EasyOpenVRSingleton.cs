using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Valve.VR;

namespace BOLL7708
{
    public sealed class EasyOpenVRSingleton
    {
        /**
         * This is a singleton because in my own experience connecting multiple 
         * times to OpenVR from the same application is a terrible idea.
         */
        private static EasyOpenVRSingleton __instance = null;
        private EasyOpenVRSingleton() { }

        private bool _debug = true;
        private Random _rnd = new Random();
        private EVRApplicationType _appType = EVRApplicationType.VRApplication_Background;
        private Action<string> _debugLogAction = null;

        public static EasyOpenVRSingleton Instance
        {
            get
            {
                if (__instance == null) __instance = new EasyOpenVRSingleton();
                return __instance;
            }
        }
        #region setup
        public void SetApplicationType(EVRApplicationType appType)
        {
            _appType = appType;
        }

        /**
         * Will output debug information
         */
        public void SetDebug(bool debug)
        {
            _debug = debug;
        }

        public void SetDebugLogAction(Action<string> action)
        {
            _debugLogAction = action;
        }
        #endregion

        #region init
        private uint _initState = 0;
        public bool Init()
        {
            EVRInitError error = EVRInitError.Unknown;
            try {
                _initState = OpenVR.InitInternal(ref error, _appType);
            }
            catch (Exception e)
            {
                DebugLog(e, "You might be building for 32bit with a 64bit .dll, error");
            }
            DebugLog(error);
            return error == EVRInitError.None && _initState > 0;
        }
        public bool IsInitialized()
        {
            return _initState > 0;
        }
        #endregion

        #region statistics
        public Compositor_CumulativeStats GetCumulativeStats()
        {
            Compositor_CumulativeStats stats = new Compositor_CumulativeStats();
            OpenVR.Compositor.GetCumulativeStats(ref stats, (uint)Marshal.SizeOf(stats));
            return stats;
        }

        public Compositor_FrameTiming GetFrameTiming()
        {
            Compositor_FrameTiming timing = new Compositor_FrameTiming();
            timing.m_nSize = (uint)Marshal.SizeOf(timing);
            var success = OpenVR.Compositor.GetFrameTiming(ref timing, 0);
            if (!success) DebugLog("Could not get frame timing.");
            return timing;
        }

        public Compositor_FrameTiming[] GetFrameTimings(uint count)
        {
            Compositor_FrameTiming[] timings = new Compositor_FrameTiming[count];
            var resultCount = OpenVR.Compositor.GetFrameTimings(timings);
            if (resultCount == 0) DebugLog("Could not get frame timings.");
            return timings;
        }
        #endregion

        #region tracking
        public TrackedDevicePose_t[] GetDeviceToAbsoluteTrackingPose(ETrackingUniverseOrigin origin = ETrackingUniverseOrigin.TrackingUniverseStanding)
        {
            TrackedDevicePose_t[] trackedDevicePoses = new TrackedDevicePose_t[OpenVR.k_unMaxTrackedDeviceCount];
            OpenVR.System.GetDeviceToAbsoluteTrackingPose(origin, 0.0f, trackedDevicePoses);
            return trackedDevicePoses;
        }
        #endregion

        #region chaperone
        public HmdQuad_t GetPlayAreaRect()
        {
            HmdQuad_t rect = new HmdQuad_t();
            var success = OpenVR.Chaperone.GetPlayAreaRect(ref rect);
            if (!success) DebugLog("Failure getting PlayAreaRect");
            return rect;
        }

        public HmdVector2_t GetPlayAreaSize()
        {
            var size = new HmdVector2_t();
            var success = OpenVR.Chaperone.GetPlayAreaSize(ref size.v0, ref size.v1);
            if (!success) DebugLog("Failure getting PlayAreaSize");
            return size;
        }

        public bool MoveUniverse(HmdVector3_t offset, bool moveChaperone = true, bool moveLiveZeroPose = true)
        {
            OpenVR.ChaperoneSetup.RevertWorkingCopy(); // Sets working copy to current live settings
            if (moveLiveZeroPose) MoveLiveZeroPose(offset);
            if (moveChaperone) MoveChaperoneBounds(Utils.InvertVector(offset));
            var success = OpenVR.ChaperoneSetup.CommitWorkingCopy(EChaperoneConfigFile.Live); // Apply changes to live settings
            if (!success) DebugLog("Failure to commit Chaperone changes.");
            return success;
        }

        public bool MoveChaperoneBounds(HmdVector3_t offset)
        {
            HmdQuad_t[] physQuad;
            var success = OpenVR.ChaperoneSetup.GetWorkingCollisionBoundsInfo(out physQuad);
            if (!success) DebugLog("Failure to load Chaperone bounds.");

            for (int i = 0; i < physQuad.Length; i++)
            {
                MoveCorner(ref physQuad[i].vCorners0);
                MoveCorner(ref physQuad[i].vCorners1);
                MoveCorner(ref physQuad[i].vCorners2);
                MoveCorner(ref physQuad[i].vCorners3);
            }
            OpenVR.ChaperoneSetup.SetWorkingCollisionBoundsInfo(physQuad);

            void MoveCorner(ref HmdVector3_t corner)
            {
                // Will not change points at vertical 0, that's the bottom of the Chaperone.
                // This as it appears the bottom gets reset to 0 at a regular interval anyway.
                corner.v0 += offset.v0;
                if (corner.v1 != 0) corner.v1 += offset.v1;
                corner.v2 += offset.v2;
            }
            return success;
        }

        public void MoveLiveZeroPose(HmdVector3_t offset)
        {
            var standingPos = new HmdMatrix34_t();
            var sittingPos = new HmdMatrix34_t();

            OpenVR.ChaperoneSetup.GetWorkingStandingZeroPoseToRawTrackingPose(ref standingPos);
            OpenVR.ChaperoneSetup.GetWorkingSeatedZeroPoseToRawTrackingPose(ref sittingPos);

            // As the zero pose is relative to the unvierse calibration and not the play area 
            // we need to adjust the offset with the rotation of the universe.
            offset = Utils.MultiplyVectorWithRotationMatrix(offset, standingPos);
            standingPos.m3 += offset.v0;
            standingPos.m7 += offset.v1;
            standingPos.m11 += offset.v2;
            sittingPos.m3 += offset.v0;
            sittingPos.m7 += offset.v1;
            sittingPos.m11 += offset.v2;

            OpenVR.ChaperoneSetup.SetWorkingStandingZeroPoseToRawTrackingPose(ref standingPos);
            OpenVR.ChaperoneSetup.SetWorkingSeatedZeroPoseToRawTrackingPose(ref sittingPos);
        }
        #endregion

        #region controller
        /*
         * Includes things like analogue axes of triggers, pads & sticks
         * OBS: Deprecated
         */
        public VRControllerState_t GetControllerState(uint index)
        {
            VRControllerState_t state = new VRControllerState_t();
            var success = OpenVR.System.GetControllerState(index, ref state, (uint)Marshal.SizeOf(state));
            if (!success) DebugLog("Failure getting ControllerState");
            return state;
        }

        /**
         * Will return the index of the role if found
         * Useful if you want to know which controller is right or left.
         * Note: Will eventually be removed as it has now been deprecated.
         */
        public uint GetIndexForControllerRole(ETrackedControllerRole role)
        {
            return OpenVR.System.GetTrackedDeviceIndexForControllerRole(role);
        }
        #endregion

        #region tracked_device
        public uint[] GetIndexesForTrackedDeviceClass(ETrackedDeviceClass _class)
        {
            // Not sure how this one works, no ref? Skip for now.
            // var result = new uint[OpenVR.k_unMaxTrackedDeviceCount];
            // var count = OpenVR.System.GetSortedTrackedDeviceIndicesOfClass(_class, result, uint.MaxValue);
            var result = new List<uint>();
            for (uint i = 0; i < OpenVR.k_unMaxTrackedDeviceCount; i++)
            {
                if (GetTrackedDeviceClass(i) == _class) result.Add(i);
            }
            return result.ToArray();
        }

        public ETrackedDeviceClass GetTrackedDeviceClass(uint index)
        {
            return OpenVR.System.GetTrackedDeviceClass(index);
        }

        /*
         * Example of property: ETrackedDeviceProperty.Prop_DeviceBatteryPercentage_Float
         */
        public float GetFloatTrackedDeviceProperty(uint index, ETrackedDeviceProperty property)
        {
            var error = new ETrackedPropertyError();
            var result = OpenVR.System.GetFloatTrackedDeviceProperty(index, property, ref error);
            DebugLog(error, property);
            return result;
        }
        /*
         * Example of property: ETrackedDeviceProperty.Prop_SerialNumber_String
         */
        public string GetStringTrackedDeviceProperty(uint index, ETrackedDeviceProperty property)
        {
            var error = new ETrackedPropertyError();
            StringBuilder sb = new StringBuilder((int)OpenVR.k_unMaxPropertyStringSize);
            OpenVR.System.GetStringTrackedDeviceProperty(index, property, sb, OpenVR.k_unMaxPropertyStringSize, ref error);
            DebugLog(error);
            return sb.ToString();
        }


        /*
         * Example of property: ETrackedDeviceProperty.Prop_EdidProductID_Int32
         */
        public int GetIntegerTrackedDeviceProperty(uint index, ETrackedDeviceProperty property)
        {
            var error = new ETrackedPropertyError();
            var result = OpenVR.System.GetInt32TrackedDeviceProperty(index, property, ref error);
            DebugLog(error);
            return result;
        }

        /*
         * Example of property: ETrackedDeviceProperty.Prop_CurrentUniverseId_Uint64
         */
        public ulong GetLongTrackedDeviceProperty(uint index, ETrackedDeviceProperty property)
        {
            var error = new ETrackedPropertyError();
            var result = OpenVR.System.GetUint64TrackedDeviceProperty(index, property, ref error);
            DebugLog(error);
            return result;
        }

        /*
         * Example of property: ETrackedDeviceProperty.Prop_ContainsProximitySensor_Bool
         */
        public bool GetBooleanTrackedDeviceProperty(uint index, ETrackedDeviceProperty property)
        {
            var error = new ETrackedPropertyError();
            var result = OpenVR.System.GetBoolTrackedDeviceProperty(index, property, ref error);
            DebugLog(error);
            return result;
        }

        // TODO: This has apparently been deprecated, figure out how to do it with the new input system.
        public void TriggerHapticPulseInController(ETrackedControllerRole role)
        {
            var index = GetIndexForControllerRole(role);
            OpenVR.System.TriggerHapticPulse(index, 0, 10000); // This works: https://github.com/ValveSoftware/openvr/wiki/IVRSystem::TriggerHapticPulse
        }

        public InputOriginInfo_t GetOriginTrackedDeviceInfo(ulong originHandle) {
            var info = new InputOriginInfo_t();
            var error = OpenVR.Input.GetOriginTrackedDeviceInfo(originHandle, ref info, (uint)Marshal.SizeOf(info));
            DebugLog(error);
            return info;
        }
        #endregion

        #region events
        private Dictionary<EVREventType, List<Action<VREvent_t>>> _events = new Dictionary<EVREventType, List<Action<VREvent_t>>>();

        ///<summary>Register an event that should trigger an action, run UpdateEvents() to get new events.</summary>
        public void RegisterEvent(EVREventType type, Action<VREvent_t> action)
        {
            RegisterEvents(new EVREventType[1] { type }, action);
        }
        /**
         * Register multiple events that will trigger the same action.
         */
        public void RegisterEvents(EVREventType[] types, Action<VREvent_t> action)
        {
            foreach (var t in types)
            {
                if (!_events.ContainsKey(t)) _events.Add(t, new List<Action<VREvent_t>>());
                _events[t].Add(action);
            }
        }

        /// <summary>Load new events and match them against registered events types, trigger actions.</summary>
        public void UpdateEvents(bool debugUnhandledEvents = false)
        {
            var events = GetNewEvents();
            foreach (var e in events)
            {
                var type = (EVREventType)e.eventType;
                if (_events.ContainsKey(type))
                {
                    foreach (var action in _events[type]) action.Invoke(e);
                }
                else if (debugUnhandledEvents) DebugLog((EVREventType)e.eventType, "Unhandled event");
            }
        }

        ///<summary>Will get all new events in the queue, note that this will cancel out triggering any registered events when running UpdateEvents().</summary>
        public VREvent_t[] GetNewEvents()
        {
            var vrEvents = new List<VREvent_t>();
            var vrEvent = new VREvent_t();
            uint eventSize = (uint)Marshal.SizeOf(vrEvent);
            try
            {
                while (OpenVR.System.PollNextEvent(ref vrEvent, eventSize))
                {
                    vrEvents.Add(vrEvent);
                }
            } catch (Exception e)
            {
                DebugLog(e, "Could not get new events");
            }

            return vrEvents.ToArray();
        }
        #endregion

        #region input

        /**
         * From the SteamVR Unity Plugin: https://github.com/ValveSoftware/steamvr_unity_plugin/blob/master/Assets/SteamVR/Input/SteamVR_Input_Sources.cs
         * Used to get the handle for any specific input source.
         */
        public enum InputSource
        {
            [Description("/unrestricted")]
            Any,

            [Description(OpenVR.k_pchPathUserHandLeft)]
            LeftHand,
            [Description(OpenVR.k_pchPathUserElbowLeft)]
            LeftElbow,
            [Description(OpenVR.k_pchPathUserShoulderLeft)]
            LeftShoulder,
            [Description(OpenVR.k_pchPathUserKneeLeft)]
            LeftKnee,
            [Description(OpenVR.k_pchPathUserFootLeft)]
            LeftFoot,

            [Description(OpenVR.k_pchPathUserHandRight)]
            RightHand,
            [Description(OpenVR.k_pchPathUserElbowRight)]
            RightElbow,
            [Description(OpenVR.k_pchPathUserShoulderRight)]
            RightShoulder,
            [Description(OpenVR.k_pchPathUserKneeRight)]
            RightKnee,
            [Description(OpenVR.k_pchPathUserFootRight)]
            RightFoot,

            [Description(OpenVR.k_pchPathUserHead)]
            Head,
            [Description(OpenVR.k_pchPathUserChest)]
            Chest,
            [Description(OpenVR.k_pchPathUserWaist)]
            Waist,

            [Description(OpenVR.k_pchPathUserGamepad)]
            Gamepad,
            [Description(OpenVR.k_pchPathUserStylus)]
            Stylus,
            [Description(OpenVR.k_pchPathUserKeyboard)]
            Keyboard,

            [Description(OpenVR.k_pchPathUserCamera)]
            Camera,
            [Description(OpenVR.k_pchPathUserTreadmill)]
            Treadmill,
        }

        public enum InputType
        {
            Analog,
            Digital,
            Pose
        }
        private class InputAction
        {
            internal string path;
            internal object data;
            internal InputType type;
            internal object action;
            internal ulong handle = 0;
            internal string pathEnd = "";
            internal bool isChord = false; // Needed to avoid filtering on the input source handle as Chords can flip their on/off action between sources depending on which button is activated/deactivated first.

            internal InputActionInfo getInfo(ulong sourceHandle)
            {
                return new InputActionInfo
                {
                    handle = handle,
                    path = path,
                    pathEnd = pathEnd,
                    sourceHandle = sourceHandle
                };
            }
        }

        public class InputActionInfo
        {
            public ulong handle;
            public string path;
            public string pathEnd;
            public ulong sourceHandle;
        }

        private List<InputAction> _inputActions = new List<InputAction>();
        private List<VRActiveActionSet_t> _inputActionSets = new List<VRActiveActionSet_t>();

        /**
         * Load the actions manifest to register actions for the application
         * OBS: Make sure the encoding is UTF8 and not UTF8+BOM
         */
        public EVRInputError LoadActionManifest(string relativePath)
        {
            return OpenVR.Input.SetActionManifestPath(Path.GetFullPath(relativePath));
        }

        public bool RegisterActionSet(string path)
        {
            ulong handle = 0;
            var error = OpenVR.Input.GetActionSetHandle(path, ref handle);
            if (handle != 0 && error == EVRInputError.None)
            {
                var actionSet = new VRActiveActionSet_t
                {
                    ulActionSet = handle,
                    ulRestrictedToDevice = OpenVR.k_ulInvalidActionSetHandle,
                    nPriority = 0
                };
                _inputActionSets.Add(actionSet);
            }
            return DebugLog(error);
        }

        private EVRInputError RegisterAction(ref InputAction ia)
        {
            ulong handle = 0;
            var error = OpenVR.Input.GetActionHandle(ia.path, ref handle);
            var pathParts = ia.path.Split('/');
            if (handle != 0 && error == EVRInputError.None)
            {
                ia.handle = handle;
                ia.pathEnd = pathParts[pathParts.Length - 1];
                _inputActions.Add(ia);
            }
            else DebugLog(error);
            return error;
        }

        public void ClearInputActions()
        {
            _inputActionSets.Clear();
            _inputActions.Clear();
        }

        /**
         * Register an analog action with a callback action
         */
        public bool RegisterAnalogAction(string path, Action<InputAnalogActionData_t, InputActionInfo> action, bool isChord = false)
        {
            var ia = new InputAction
            {
                path = path,
                type = InputType.Analog,
                action = action,
                data = new InputAnalogActionData_t(),
                isChord = isChord
            };
            var error = RegisterAction(ref ia);
            return DebugLog(error);
        }

        /**
         * Register a digital action with a callback action
         */
        public bool RegisterDigitalAction(string path, Action<InputDigitalActionData_t, InputActionInfo> action, bool isChord = false)
        {
            var inputAction = new InputAction
            {
                path = path,
                type = InputType.Digital,
                action = action,
                data = new InputDigitalActionData_t(),
                isChord = isChord
            };
            var error = RegisterAction(ref inputAction);
            return DebugLog(error);
        }

        /**
         * Register a digital action with a callback action
         */
        public bool RegisterPoseAction(string path, Action<InputPoseActionData_t, InputActionInfo> action, bool isChord = false)
        {
            var inputAction = new InputAction
            {
                path = path,
                type = InputType.Pose,
                action = action,
                data = new InputPoseActionData_t(),
                isChord = isChord
            };
            var error = RegisterAction(ref inputAction);
            return DebugLog(error);
        }

        /**
         * Retrieve the handle for the input source of a specific input device
         */
        public ulong GetInputSourceHandle(InputSource inputSource)
        {

            DescriptionAttribute[] attributes = (DescriptionAttribute[])inputSource
                .GetType()
                .GetField(inputSource.ToString())
                .GetCustomAttributes(typeof(DescriptionAttribute), false);
            var source = attributes.Length > 0 ? attributes[0].Description : string.Empty;

            ulong handle = 0;
            var error = OpenVR.Input.GetInputSourceHandle(source, ref handle);
            DebugLog(error);
            return handle;
        }


        /**
         * Update all action states, this will trigger stored actions if needed.
         * Digital actions triggers on change, analog actions every update.
         * OBS: Only run this once per update, or you'll get no input data at all.
         */
        public bool UpdateActionStates(ulong[] inputSourceHandles = null)
        {
            if (inputSourceHandles == null) inputSourceHandles = new ulong[] { OpenVR.k_ulInvalidPathHandle };
            var error = OpenVR.Input.UpdateActionState(_inputActionSets.ToArray(), (uint)Marshal.SizeOf(typeof(VRActiveActionSet_t)));

            _inputActions.ForEach((InputAction action) =>
            {
                switch (action.type)
                {
                    case InputType.Analog:
                        foreach (var handle in inputSourceHandles) GetAnalogAction(action, handle);
                        break;
                    case InputType.Digital:
                        foreach (var handle in inputSourceHandles) GetDigitalAction(action, handle);
                        break;
                    case InputType.Pose:
                        foreach (var handle in inputSourceHandles) GetPoseAction(action, handle);
                        break;
                }
            });
            return DebugLog(error);
        }

        private bool GetAnalogAction(InputAction inputAction, ulong inputSourceHandle)
        {
            if (inputAction.isChord) inputSourceHandle = 0;
            var size = (uint)Marshal.SizeOf(typeof(InputAnalogActionData_t));
            var data = (InputAnalogActionData_t)inputAction.data;
            var error = OpenVR.Input.GetAnalogActionData(inputAction.handle, ref data, size, inputSourceHandle);
            var action = ((Action<InputAnalogActionData_t, InputActionInfo>)inputAction.action);
            if(data.bActive) action.Invoke(data, inputAction.getInfo(inputSourceHandle));
            return DebugLog(error, $"handle: {inputAction.handle}, error");
        }

        private bool GetDigitalAction(InputAction inputAction, ulong inputSourceHandle)
        {
            if (inputAction.isChord) inputSourceHandle = 0;
            var size = (uint)Marshal.SizeOf(typeof(InputDigitalActionData_t));
            var data = (InputDigitalActionData_t)inputAction.data;
            var error = OpenVR.Input.GetDigitalActionData(inputAction.handle, ref data, size, inputSourceHandle);
            var action = ((Action<InputDigitalActionData_t, InputActionInfo>)inputAction.action);
            if (data.bActive && data.bChanged) action.Invoke(data, inputAction.getInfo(inputSourceHandle));
            return DebugLog(error, $"handle: {inputAction.handle}, error");
        }

        private bool GetPoseAction(InputAction inputAction, ulong inputSourceHandle)
        {
            if (inputAction.isChord) inputSourceHandle = 0;
            var size = (uint)Marshal.SizeOf(typeof(InputPoseActionData_t));
            var data = (InputPoseActionData_t)inputAction.data;
            var error = OpenVR.Input.GetPoseActionDataRelativeToNow(inputAction.handle, ETrackingUniverseOrigin.TrackingUniverseStanding, 0f, ref data, size, inputSourceHandle);
            var action = ((Action<InputPoseActionData_t, InputActionInfo>)inputAction.action);
            if (data.bActive) action.Invoke(data, inputAction.getInfo(inputSourceHandle));
            return DebugLog(error, $"handle: {inputAction.handle}, error");
        }
        #endregion

        #region screenshots
        public class ScreenshotResult
        {
            public uint handle;
            public EVRScreenshotType type;
            public string filePath;
            public string filePathVR;
        }

        /*
         * Set screenshot path, if not set they will end up in: %programfiles(x86)%\Steam\steamapps\common\SteamVR\bin\
         * Returns false if the directory does not exist.
         */
        private string _screenshotPath = "";
        public bool SetScreenshotOutputFolder(string path)
        {
            var exists = Directory.Exists(path);
            if (exists) _screenshotPath = path;
            return exists;
        }

        /*
         * Hooks the screenshot function so it overrides the built in screenshot shortcut in SteamVR!
         * Listen to the VREvent_ScreenshotTriggered event to know when to acquire a screenshot.
         */
        public bool HookScreenshots()
        {
            EVRScreenshotType[] arr = { EVRScreenshotType.Stereo };
            var error = OpenVR.Screenshots.HookScreenshot(arr);
            return DebugLog(error);
        }

        private Tuple<string, string> GetScreenshotPaths(string prefix, string postfix, string timestampFormat = "yyyyMMdd_HHmmss_fff")
        {
            var screenshotPath = _screenshotPath;
            if (screenshotPath != string.Empty) screenshotPath = $"{screenshotPath}\\";
            if (prefix != string.Empty) prefix = $"{prefix}_";
            if (postfix != string.Empty) postfix = $"_{postfix}";
            var timestamp = DateTime.Now.ToString(timestampFormat);

            var filePath = $"{screenshotPath}{prefix}{timestamp}{postfix}";
            var filePathVR = $"{screenshotPath}{prefix}{timestamp}_vr{postfix}";

            return new Tuple<string, string>(filePath, filePathVR);
        }

        /**
         * Takes a stereo screenshot, works with all applications as it grabs render output directly.
         * 
         * OBS: Requires a scene application to be running, else screenshot functionality will stop working.
         */
        public bool TakeScreenshot(
            out ScreenshotResult screenshotResult,
            string prefix = "",
            string postfix = "")
        {
            uint handle = 0;
            var filePaths = GetScreenshotPaths(prefix, postfix);
            var type = EVRScreenshotType.Stereo;
            var error = OpenVR.Screenshots.TakeStereoScreenshot(ref handle, filePaths.Item1, filePaths.Item2);
            screenshotResult =
                error == EVRScreenshotError.None ?
                new ScreenshotResult {
                    handle = handle,
                    type = type,
                    filePath = filePaths.Item1,
                    filePathVR = filePaths.Item2
                } : null;
            return DebugLog(error);
        }

        /**
         * Use this to request other types of screenshots.
         * 
         * OBS: This will NOT WORK if you have hooked the system screenshot function, 
         * it will seemingly leave a screenshot request in limbo preventing future screenshots.
         */
        public bool RequestScreenshot(
            out ScreenshotResult screenshotResult,
            string prefix = "",
            string postfix = "",
            EVRScreenshotType screenshotType = EVRScreenshotType.Stereo)
        {
            var filePaths = GetScreenshotPaths(prefix, postfix);
            uint handle = 0;
            var error = OpenVR.Screenshots.RequestScreenshot(ref handle, screenshotType, filePaths.Item1, filePaths.Item2);
            screenshotResult =
                error == EVRScreenshotError.None ?
                new ScreenshotResult {
                    handle = handle,
                    type = screenshotType,
                    filePath = filePaths.Item1,
                    filePathVR = filePaths.Item2
                } : null;
            return DebugLog(error);
        }

        /*
         * This will attempt to submit the screenshot to Steam to be in the screenshot library for the current scene application.
         */
        public bool SubmitScreenshotToSteam(ScreenshotResult screenshotResult)
        {
            var error = OpenVR.Screenshots.SubmitScreenshot(
                screenshotResult.handle,
                screenshotResult.type,
                $"{screenshotResult.filePath}.png",
                $"{screenshotResult.filePathVR}.png"
            );
            return DebugLog(error);
        }

        #endregion

        #region video
        public float GetRenderTargetForCurrentApp()
        {
            return GetFloatSetting(OpenVR.k_pch_SteamVR_Section, OpenVR.k_pch_SteamVR_SupersampleScale_Float);
        }

        public bool GetSuperSamplingEnabledForCurrentApp()
        {
            return GetBoolSetting(OpenVR.k_pch_SteamVR_Section, OpenVR.k_pch_SteamVR_SupersampleManualOverride_Bool);
        }

        public bool SetSuperSamplingEnabledForCurrentApp(bool enabled)
        {
            return SetBoolSetting(OpenVR.k_pch_SteamVR_Section, OpenVR.k_pch_SteamVR_SupersampleManualOverride_Bool, enabled);
        }

        public float GetSuperSamplingForCurrentApp()
        {
            return GetFloatSetting(OpenVR.k_pch_SteamVR_Section, OpenVR.k_pch_SteamVR_SupersampleScale_Float);
        }

        /**
         * Will set the render scale for the current app
         * scale 1 = 100%
         * OBS: Will enable super sampling override if it is not enabled
         */
        public bool SetSuperSamplingForCurrentApp(float scale)
        {
            return SetFloatSetting(OpenVR.k_pch_SteamVR_Section, OpenVR.k_pch_SteamVR_SupersampleScale_Float, scale);
        }
        #endregion

        #region notifications
        /*
         * Thank you artumino and in extension Marlamin on GitHub for their public code which I referenced for notifications.
         * Also thanks to Valve for finally adding the interface for notifications to the C# header file.
         * 
         * In reality I tried implementing notifications back in April 2016, poked Valve about it in October the same year,
         * pointed out what was missing in May and December 2017, yet again in January 2019 and boom, now we have it!
         */

        private List<uint> _notifications = new List<uint>();

        /*
         * We initialize an overlay to display notifications with.
         * The title will be visible above the notification.
         * Returns the handle used to send notifications, 0 on fail.
         */
        public ulong InitNotificationOverlay(string notificationTitle)
        {
            ulong handle = 0;
            var key = Guid.NewGuid().ToString();
            var error = OpenVR.Overlay.CreateOverlay(key, notificationTitle, ref handle);
            if (DebugLog(error)) return handle;
            return 0;
        }

        public uint EnqueueNotification(ulong overlayHandle, string message)
        {
            return EnqueueNotification(overlayHandle, message, new NotificationBitmap_t());
        }

        public uint EnqueueNotification(ulong overlayHandle, string message, NotificationBitmap_t bitmap)
        {
            return EnqueueNotification(overlayHandle, EVRNotificationType.Transient, message, EVRNotificationStyle.Application, bitmap);
        }

        /*
         * Will enqueue a notification to be displayed in the headset.
         * Returns ID for this specific notification.
         */
        public uint EnqueueNotification(ulong overlayHandle, EVRNotificationType type, string message, EVRNotificationStyle style, NotificationBitmap_t bitmap)
        {
            uint id = 0;
            while (id == 0 || _notifications.Contains(id)) id = (uint)_rnd.Next(); // Not sure why we do this
            var error = OpenVR.Notifications.CreateNotification(overlayHandle, 0, type, message, style, ref bitmap, ref id);
            DebugLog(error);
            _notifications.Add(id);
            return id;
        }

        /*
         * Used to dismiss a persistent notification.
         */
        public bool DismissNotification(uint id, out EVRNotificationError error)
        {
            error = OpenVR.Notifications.RemoveNotification(id);
            if (error == EVRNotificationError.OK) _notifications.Remove(id);
            return DebugLog(error);
        }

        public bool EmptyNotificationsQueue()
        {
            var error = EVRNotificationError.OK;
            var success = true;
            foreach (uint id in _notifications)
            {
                error = OpenVR.Notifications.RemoveNotification(id);
                success = DebugLog(error);
            }
            _notifications.Clear();
            return success;
        }
        #endregion

        #region Settings
        /// <summary>
        ///  Fetches a settings value from the SteamVR settings
        /// </summary>
        /// <param name="section">Example: OpenVR.k_pch_CollisionBounds_Section</param>
        /// <param name="setting">Example: OpenVR.k_pch_SteamVR_SupersampleScale_Float</param>
        /// <returns>float value</returns>
        public float GetFloatSetting(string section, string setting) {
            EVRSettingsError error = EVRSettingsError.None;
            var value = OpenVR.Settings.GetFloat(
                section,
                setting,
                ref error
            );
            DebugLog(error);
            return value;
        }

        public bool SetFloatSetting(string section, string setting, float value) {
            EVRSettingsError error = EVRSettingsError.None;
            OpenVR.Settings.SetFloat(section, setting, value, ref error);
            return DebugLog(error);
        }

        public bool GetBoolSetting(string section, string setting) {
            EVRSettingsError error = EVRSettingsError.None;
            var value = OpenVR.Settings.GetBool(
                section,
                setting,
                ref error
            );
            DebugLog(error);
            return value;
        }

        public bool SetBoolSetting(string section, string setting, bool value) {
            EVRSettingsError error = EVRSettingsError.None;
            OpenVR.Settings.SetBool(section, setting, value, ref error);
            return DebugLog(error);
        }

        public int GetIntSetting(string section, string setting)
        {
            EVRSettingsError error = EVRSettingsError.None;
            var value = OpenVR.Settings.GetInt32(
                section,
                setting,
                ref error
            );
            DebugLog(error);
            return value;
        }

        public bool SetIntSetting(string section, string setting, int value)
        {
            EVRSettingsError error = EVRSettingsError.None;
            OpenVR.Settings.SetInt32(section, setting, value, ref error);
            return DebugLog(error);
        }

        public string GetStringSetting(string section, string setting)
        {
            /* TODO: Reference Unity plugin?
            EVRSettingsError error = EVRSettingsError.None;
            var sb = new StringBuilder();
            OpenVR.Settings.GetString(
                section,
                setting,
                sb,

                ref error
            );
            DebugLog(error);
            return value;
            */
            return "";
        }

        public bool SetStringSetting(string section, string setting, string value)
        {
            EVRSettingsError error = EVRSettingsError.None;
            OpenVR.Settings.SetString(section, setting, value, ref error);
            return DebugLog(error);
        }

        #endregion

        #region overlays
        /// <summary>
        /// Creates an overlay that will show up in the headset if you draw to it
        /// </summary>
        /// <param name="uniqueKey"></param>
        /// <param name="title"></param>
        /// <param name="transform">Get an empty transform from Utils.GetEmptyTransform</param>
        /// <param name="width">Default is 1, height is derived from the texture aspect ratio and the width</param>
        /// <param name="anchor">Default is none, else index for which tracked device to attach overlay to</param>
        /// <param name="origin">If we have no anchor, we need an origin to set position, defaults to standing</param>
        /// <returns>0 if we failed to create an overlay</returns>
        public ulong CreateOverlay(string uniqueKey, string title, HmdMatrix34_t transform, float width = 1, uint anchor=uint.MaxValue, ETrackingUniverseOrigin origin = ETrackingUniverseOrigin.TrackingUniverseStanding)
        {
            ulong handle = 0;
            var error = OpenVR.Overlay.CreateOverlay(uniqueKey, title, ref handle);
            if(error == EVROverlayError.None)
            {
                OpenVR.Overlay.SetOverlayWidthInMeters(handle, width);
                if (anchor != uint.MaxValue) OpenVR.Overlay.SetOverlayTransformTrackedDeviceRelative(handle, anchor, ref transform);
                else OpenVR.Overlay.SetOverlayTransformAbsolute(handle, origin, ref transform);
            }
            DebugLog(error);
            return handle;
        }

        public bool SetOverlayTransform(ulong handle, HmdMatrix34_t transform, uint anchor = uint.MaxValue, ETrackingUniverseOrigin origin = ETrackingUniverseOrigin.TrackingUniverseStanding)
        {
            EVROverlayError error;
            if (anchor != uint.MaxValue) error = OpenVR.Overlay.SetOverlayTransformTrackedDeviceRelative(handle, anchor, ref transform);
            else error = OpenVR.Overlay.SetOverlayTransformAbsolute(handle, origin, ref transform);
            return DebugLog(error);
        }

        public bool SetOverlayTextureFromFile(ulong handle, string path)
        {
            var error = OpenVR.Overlay.SetOverlayFromFile(handle, path);
            return DebugLog(error);
        }

        /// <summary>
        /// Preliminiary as I have yet to figure out how to make my own textures at runtime.
        /// </summary>
        /// <param name="handle"></param>
        /// <param name="texture"></param>
        /// <returns></returns>
        public bool SetOverlayTexture(ulong handle, ref Texture_t texture)
        {
            // DXGI_FORMAT_R8G8B8A8_UNORM 
            var error = OpenVR.Overlay.SetOverlayTexture(handle, ref texture);
            return DebugLog(error);
        }

        /// <summary>
        /// Sets raw overlay pixels from Bitmap, appears to crash íf going above 1mpix or near that.
        /// It's also said to be super inefficient by Valve themselves, so never use this for frequently updating overlays.
        /// </summary>
        /// <param name="handle"></param>
        /// <param name="bmp"></param>
        public void SetOverlayPixels(ulong handle, Bitmap bmp)
        {
            BitmapUtils.PointerFromBitmap(bmp, true, (pointer) => {
                int bytesPerPixel = Bitmap.GetPixelFormatSize(bmp.PixelFormat) / 8;
                var error = OpenVR.Overlay.SetOverlayRaw(handle, pointer, (uint) bmp.Width, (uint) bmp.Height, (uint) bytesPerPixel);
            });
        }

        public HmdMatrix34_t GetOverlayTransform(ulong handle, ETrackingUniverseOrigin origin = ETrackingUniverseOrigin.TrackingUniverseStanding)
        {
            var transform = new HmdMatrix34_t();
            var error = OpenVR.Overlay.GetOverlayTransformAbsolute(handle, ref origin, ref transform);
            DebugLog(error);
            return transform;
        }

        /// <summary>
        /// Sets the alpha of the overlay
        /// </summary>
        /// <param name="handle"></param>
        /// <param name="alpha">Normalized 0.0-1.0</param>
        /// <returns></returns>
        public bool SetOverlayAlpha(ulong handle, float alpha)
        {
            var error = OpenVR.Overlay.SetOverlayAlpha(handle, alpha);
            return DebugLog(error);
        }

        public bool SetOverlayWidth(ulong handle, float width)
        {
            var error = OpenVR.Overlay.SetOverlayWidthInMeters(handle, width);
            return DebugLog(error);
        }
        
        public bool SetOverlayVisibility(ulong handle, bool visible)
        {
            EVROverlayError error;
            if (visible) error = OpenVR.Overlay.ShowOverlay(handle);
            else error = OpenVR.Overlay.HideOverlay(handle);
            return DebugLog(error);
        }
        
        /**
         * Will have to explore this at a later date, right now my overlays are non-interactive.
         */
        public VREvent_t[] GetNewOverlayEvents(ulong overlayHandle)
        {
            var vrEvents = new List<VREvent_t>();
            var vrEvent = new VREvent_t();
            uint eventSize = (uint)Marshal.SizeOf(vrEvent);
            while (OpenVR.Overlay.PollNextOverlayEvent(overlayHandle, ref vrEvent, eventSize))
            {
                vrEvents.Add(vrEvent);
            }
            return vrEvents.ToArray();
        }
        public ulong FindOverlay(string uniqueKey)
        {
            ulong handle = 0;
            var error = OpenVR.Overlay.FindOverlay(uniqueKey, ref handle);
            DebugLog(error);
            return handle;
        }

        public class OverlayTextureSize
        {
            public uint width;
            public uint height;
            public float aspectRatio;
        }

        public OverlayTextureSize GetOverlayTextureSize(ulong handle)
        {
            uint width = 0;
            uint height = 0;
            var error = OpenVR.Overlay.GetOverlayTextureSize(handle, ref width, ref height);
            DebugLog(error);
            return (width == 0 || height == 0) ? 
                new OverlayTextureSize() : 
                new OverlayTextureSize { width=width, height=height, aspectRatio=(float)width/(float)height };
        }
        #endregion

        #region shutting down

        /*
         * Listen for a VREvent_Quit and run this afterwards for your application to not get terminated. Then run Shutdown.
         */
        public void AcknowledgeShutdown()
        {
            OpenVR.System.AcknowledgeQuit_Exiting();
        }

        /*
         * Run this after AcknowledgeShutdown and after finishing all work, or OpenVR will likely throw an exception.
         */
        public void Shutdown()
        {
            OpenVR.Shutdown();
            _initState = 0;
            _events = new Dictionary<EVREventType, List<Action<VREvent_t>>>();
            _inputActions = new List<InputAction>();
        }

        #endregion

        #region system

        /**
         * Load an app manifest for the application
         * Pretty sure this is required to show up in the input bindings interface
         * OBS: Make sure the encoding is UTF8 and not UTF8+BOM
         */
        public bool LoadAppManifest(string relativePath)
        {
            var error = OpenVR.Applications.AddApplicationManifest(Path.GetFullPath(relativePath), false);
            return DebugLog(error);
        }

        public bool RemoveAppManifest(string relativePath)
        {
            var error = OpenVR.Applications.RemoveApplicationManifest(Path.GetFullPath(relativePath));
            return DebugLog(error);
        }

        /// <summary>
        /// Will add the application manifest and optionally register for auto launch.
        /// OBS: For auto launch to work the manifest must include "is_dashboard_overlay": true.
        /// </summary>
        /// <param name="relativeManifestPath">The relative path to your application manifest</param>
        /// <param name="applicationKey">Application key, used to check if already installed.</param>
        /// <param name="alsoRegisterAutoLaunch">Optional flag to register for auto launch.</param>
        /// <returns></returns>
        public bool AddApplicationManifest(string relativeManifestPath, string applicationKey, bool alsoRegisterAutoLaunch=false) {
            if (!OpenVR.Applications.IsApplicationInstalled(applicationKey))
            {
                var manifestError = OpenVR.Applications.AddApplicationManifest(Path.GetFullPath(relativeManifestPath), false);
                if(manifestError == EVRApplicationError.None && alsoRegisterAutoLaunch)
                {
                    var autolaunchError = OpenVR.Applications.SetApplicationAutoLaunch(applicationKey, true);
                    return DebugLog(autolaunchError);
                }
                return DebugLog(manifestError);
            }
            return false;
        }
        
        /**
         * Will return the application ID for the currently running scene application.
         * Will return an empty string is there is no result.
         */
        public string GetRunningApplicationId()
        {
            var pid = OpenVR.Applications.GetCurrentSceneProcessId();
            var sb = new StringBuilder((int)OpenVR.k_unMaxApplicationKeyLength);
            var error = OpenVR.Applications.GetApplicationKeyByProcessId(pid, sb, OpenVR.k_unMaxApplicationKeyLength);
            DebugLog(error);
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
        #endregion

        #region private_utils
        private void DebugLog(string message)
        {
            if (_debug)
            {
                var st = new StackTrace();
                var sf = st.GetFrame(1);
                var methodName = sf.GetMethod().Name;
                var text = $"{methodName}: {message}";
                _debugLogAction?.Invoke(text);
                Debug.WriteLine(text);
            }
        }
        private bool DebugLog(Enum errorEnum, string message = "error")
        {
            var errorVal = Convert.ChangeType(errorEnum, errorEnum.GetTypeCode());
            var ok = (int)errorVal == 0;
            if (_debug && !ok)
            {
                var stackTrace = new StackTrace();
                var stackFrame = stackTrace.GetFrame(1);
                var methodName = stackFrame.GetMethod().Name;
                var text = $"{methodName} {message}: {Enum.GetName(errorEnum.GetType(), errorEnum)}";
                _debugLogAction?.Invoke(text);
                Debug.WriteLine(text);
            }
            return ok;
        }

        private bool DebugLog(Enum errorEnum, Enum valueEnum)
        {
            var errorVal = Convert.ChangeType(errorEnum, errorEnum.GetTypeCode());
            var ok = (int)errorVal == 0;
            if (_debug && !ok)
            {
                var stackTrace = new StackTrace();
                var stackFrame = stackTrace.GetFrame(1);
                var methodName = stackFrame.GetMethod().Name;
                var text = $"{methodName} {Enum.GetName(valueEnum.GetType(), valueEnum)}: {Enum.GetName(errorEnum.GetType(), errorEnum)}";
                _debugLogAction?.Invoke(text);
                Debug.WriteLine(text);
            }
            return ok;
        }

        private void DebugLog(Exception e, string message = "error")
        {
            if (_debug)
            {
                var st = new StackTrace();
                var sf = st.GetFrame(1);
                var methodName = sf.GetMethod().Name;
                var text = $"{methodName} {message}: {e.Message}";
                _debugLogAction?.Invoke(text);
                Debug.WriteLine(text);
            }
        }
        #endregion

        #region utils
        public class YPR
        {
            public double yaw;
            public double pitch;
            public double roll;
            public YPR() { }
            public YPR(double yaw, double pitch, double roll)
            {
                this.yaw = yaw;
                this.pitch = pitch;
                this.roll = roll;
            }
            public YPR(HmdVector3_t vec) {
                pitch = vec.v0;
                yaw = vec.v1;
                roll = vec.v2;
            }
        }

        public static class Utils
        {
            public static HmdMatrix34_t GetEmptyTransform()
            {
                var transform = new HmdMatrix34_t();
                transform.m0 = 1;
                transform.m5 = 1;
                transform.m10 = 1;
                return transform;
            }

            public static HmdMatrix34_t GetTransformFromEuler(YPR e)
            {
                // Assuming the angles are in radians.
                // Had to switch roll and pitch here to match SteamVR
                var ch = (float) Math.Cos(e.yaw);
                var sh = (float) Math.Sin(e.yaw);
                var ca = (float) Math.Cos(e.roll);
                var sa = (float) Math.Sin(e.roll);
                var cb = (float) Math.Cos(e.pitch);
                var sb = (float) Math.Sin(e.pitch);

                return new HmdMatrix34_t
                {
                    m0 = ch * ca,
                    m1 = sh * sb - ch * sa * cb,
                    m2 = ch * sa * sb + sh * cb,
                    m4 = sa,
                    m5 = ca * cb,
                    m6 = -ca * sb,
                    m8 = -sh * ca,
                    m9 = sh * sa * cb + ch * sb,
                    m10 = -sh * sa * sb + ch * cb
                };
            }

            public static HmdVector3_t InvertVector(HmdVector3_t position)
            {
                position.v0 = -position.v0;
                position.v1 = -position.v1;
                position.v2 = -position.v2;
                return position;
            }

            public static HmdVector3_t MultiplyVectorWithRotationMatrix(HmdVector3_t v, HmdMatrix34_t m)
            {
                return new HmdVector3_t
                {
                    v0 = m.m0 * v.v0 + m.m1 * v.v1 + m.m2 * v.v2,
                    v1 = m.m4 * v.v0 + m.m5 * v.v1 + m.m6 * v.v2,
                    v2 = m.m8 * v.v0 + m.m9 * v.v1 + m.m10 * v.v2
                };
            }

            public static HmdMatrix34_t AddVectorToMatrix(HmdMatrix34_t m, HmdVector3_t v) {
                var v2 = MultiplyVectorWithRotationMatrix(v, m);
                m.m3 += v2.v0;
                m.m7 += v2.v1;
                m.m11 += v2.v2;
                return m;
            }

            public static HmdMatrix34_t MultiplyMatrixWithMatrix(HmdMatrix34_t matA, HmdMatrix34_t matB)
            {
                return new HmdMatrix34_t
                {
                    // Row 0
                    m0 = matA.m0 * matB.m0 + matA.m1 * matB.m4 + matA.m2 * matB.m8,
                    m1 = matA.m0 * matB.m1 + matA.m1 * matB.m5 + matA.m2 * matB.m9,
                    m2 = matA.m0 * matB.m2 + matA.m1 * matB.m6 + matA.m2 * matB.m10,
                    m3 = matA.m0 * matB.m3 + matA.m1 * matB.m7 + matA.m2 * matB.m11 + matA.m3,
                    
                    // Row 1
                    m4 = matA.m4 * matB.m0 + matA.m5 * matB.m4 + matA.m6 * matB.m8,
                    m5 = matA.m4 * matB.m1 + matA.m5 * matB.m5 + matA.m6 * matB.m9,
                    m6 = matA.m4 * matB.m2 + matA.m5 * matB.m6 + matA.m6 * matB.m10,
                    m7 = matA.m4 * matB.m3 + matA.m5 * matB.m7 + matA.m6 * matB.m11 + matA.m7,
                        
                    // Row 2
                    m8 = matA.m8 * matB.m0 + matA.m9 * matB.m4 + matA.m10 * matB.m8,
                    m9 = matA.m8 * matB.m1 + matA.m9 * matB.m5 + matA.m10 * matB.m9,
                    m10 = matA.m8 * matB.m2 + matA.m9 * matB.m6 + matA.m10 * matB.m10,
                    m11 = matA.m8 * matB.m3 + matA.m9 * matB.m7 + matA.m10 * matB.m11 + matA.m11,
                };
            }

            // Dunno if you like having this here, but it helped me with debugging.
            public static string MatToString(HmdMatrix34_t mat)
            {
                return $"[{mat.m0}, {mat.m1}, {mat.m2}, {mat.m3},\n" +
                       $"{mat.m4}, {mat.m5}, {mat.m6}, {mat.m7},\n" +
                       $"{mat.m8}, {mat.m9}, {mat.m10}, {mat.m11}]";
            }

            public static HmdQuaternion_t QuaternionFromMatrix(HmdMatrix34_t m)
            {
                var w = Math.Sqrt(1 + m.m0 + m.m5 + m.m10) / 2.0;
                return new HmdQuaternion_t
                {
                    w = w, // Scalar
                    x = (m.m9 - m.m6) / (4 * w),
                    y = (m.m2 - m.m8) / (4 * w),
                    z = (m.m4 - m.m1) / (4 * w)
                };
            }

            public static YPR RotationMatrixToYPR(HmdMatrix34_t m)
            {
                // Had to switch roll and pitch here to match SteamVR
                var q = QuaternionFromMatrix(m);
                double test = q.x * q.y + q.z * q.w;
                if (test > 0.499)
                { // singularity at north pole
                    return new YPR
                    {
                        yaw = 2 * Math.Atan2(q.x, q.w), // heading
                        roll = Math.PI / 2, // attitude
                        pitch = 0 // bank
                    };
                }
                if (test < -0.499)
                { // singularity at south pole
                    return new YPR
                    {
                        yaw = -2 * Math.Atan2(q.x, q.w), // headingq
                        roll = -Math.PI / 2, // attitude
                        pitch = 0 // bank
                    };
                }
                double sqx = q.x * q.x;
                double sqy = q.y * q.y;
                double sqz = q.z * q.z;
                return new YPR
                {
                    yaw = Math.Atan2(2 * q.y * q.w - 2 * q.x * q.z, 1 - 2 * sqy - 2 * sqz), // heading
                    roll = Math.Asin(2 * test), // attitude
                    pitch = Math.Atan2(2 * q.x * q.w - 2 * q.y * q.z, 1 - 2 * sqx - 2 * sqz) // bank
                };
            }

            #region Measurement
            /// <summary>
            /// Returns the angle between two matrices in degrees.
            /// </summary>
            /// <param name="matOrigin"></param>
            /// <param name="matTarget"></param>
            /// <returns></returns>
            public static double AngleBetween(HmdMatrix34_t matOrigin, HmdMatrix34_t matTarget)
            {
                var vecOrigin = GetUnitVec3();
                var vecTarget = GetUnitVec3();                
                vecOrigin = MultiplyVectorWithRotationMatrix(vecOrigin, matOrigin);
                vecTarget = MultiplyVectorWithRotationMatrix(vecTarget, matTarget);
                var vecSize = 1.0;
                return Math.Acos(DotProduct(vecOrigin, vecTarget) / Math.Pow(vecSize, 2)) * (180/Math.PI);
            }

            private static HmdVector3_t GetUnitVec3() {
                return new HmdVector3_t() { v0 = 0, v1 = 0, v2 = 1 };
            }

            private static double DotProduct(HmdVector3_t v1, HmdVector3_t v2)
            {
                return v1.v0 * v2.v0 + v1.v1 * v2.v1 + v1.v2 * v2.v2;
            }
            #endregion
        }

        public static class BitmapUtils
        {
            /// <summary>
            /// Generate the needed bitmap for SteamVR notifications
            /// By default we flip red and blue image channels as that seems to always be required for it to display properly
            /// </summary>
            /// <param name="bmp">The system bitmap</param>
            /// <param name="flipRnB">Whether we should flip red and blue channels or not</param>
            /// <returns></returns>
            public static NotificationBitmap_t NotificationBitmapFromBitmap(Bitmap bmp, bool flipRnB=true)
            {
                return NotificationBitmapFromBitmapData(BitmapDataFromBitmap(bmp, flipRnB));
            }

            public static BitmapData BitmapDataFromBitmap(Bitmap bmpIn, bool flipRnB=false)
            {
                Bitmap bmp = (Bitmap)bmpIn.Clone();
                if (flipRnB) RGBtoBGR(bmp);
                BitmapData texData = bmp.LockBits(
                    new Rectangle(0, 0, bmp.Width, bmp.Height),
                    ImageLockMode.ReadOnly,
                    PixelFormat.Format32bppArgb
                );
                return texData;
            }

            public static NotificationBitmap_t NotificationBitmapFromBitmapData(BitmapData TextureData)
            {
                NotificationBitmap_t notification_icon = new NotificationBitmap_t();
                notification_icon.m_pImageData = TextureData.Scan0;
                notification_icon.m_nWidth = TextureData.Width;
                notification_icon.m_nHeight = TextureData.Height;
                notification_icon.m_nBytesPerPixel = 4;
                return notification_icon;
            }

            public static void PointerFromBitmap(Bitmap bmpIn, bool flipRnB, Action<IntPtr> action)
            {
                Bitmap bmp = (Bitmap)bmpIn.Clone();
                if (flipRnB) RGBtoBGR(bmp);
                BitmapData data = bmp.LockBits(new Rectangle(0, 0, bmp.Width, bmp.Height), ImageLockMode.ReadWrite, bmp.PixelFormat);
                IntPtr pointer = data.Scan0;
                action.Invoke(pointer);
                bmp.UnlockBits(data);
            }

            private static void RGBtoBGR(Bitmap bmp)
            {
                // based on https://docs.microsoft.com/en-us/dotnet/api/system.drawing.bitmap.unlockbits?view=netframework-4.8

                int bytesPerPixel = Bitmap.GetPixelFormatSize(bmp.PixelFormat) / 8;
                BitmapData data = bmp.LockBits(new Rectangle(0, 0, bmp.Width, bmp.Height), ImageLockMode.ReadWrite, bmp.PixelFormat);
                int bytes = Math.Abs(data.Stride) * bmp.Height;

                IntPtr ptr = data.Scan0;
                var rgbValues = new byte[bytes];
                Marshal.Copy(data.Scan0, rgbValues, 0, bytes);
                for (int i = 0; i < bytes; i += bytesPerPixel)
                {
                    byte dummy = rgbValues[i];
                    rgbValues[i] = rgbValues[i + 2];
                    rgbValues[i + 2] = dummy;
                }
                Marshal.Copy(rgbValues, 0, ptr, bytes);
                bmp.UnlockBits(data);
            }
        }

        public static class UnityUtils
        {
            public static HmdQuaternion_t MatrixToRotation(HmdMatrix34_t m)
            {
                // x and y are reversed to flip the rotation in the X axis, to convert OpenVR to Unity
                var q = new HmdQuaternion_t();
                q.w = Math.Sqrt(1.0f + m.m0 + m.m5 + m.m10) / 2.0f;
                q.x = -((m.m9 - m.m6) / (4 * q.w));
                q.y = -((m.m2 - m.m8) / (4 * q.w));
                q.z = (m.m4 - m.m1) / (4 * q.w);
                return q;
            }

            public static HmdVector3_t MatrixToPosition(HmdMatrix34_t m)
            {
                // m11 is reversed to flip the Z axis, to convert OpenVR to Unity
                var v = new HmdVector3_t();
                v.v0 = m.m3;
                v.v1 = m.m7;
                v.v2 = -m.m11;
                return v;
            }
        }

        #endregion
    }

    public static class Extensions
    {
        #region Translation
        
        public static HmdMatrix34_t Translate(this HmdMatrix34_t mat, HmdVector3_t v, bool localAxis = true)
        {
            if (!localAxis) return mat.Add(v);
            
            var translationMatrix = new HmdMatrix34_t
            {
                m0 = 1,
                m5 = 1,
                m10 = 1,
                m3 = v.v0,
                m7 = v.v1,
                m11 = v.v2
            };

            return mat.Multiply(translationMatrix);
        }
        
        public static HmdMatrix34_t Translate(this HmdMatrix34_t mat, float x, float y, float z, bool localAxis = true)
        {
            var translationVector = new HmdVector3_t
            {
                v0 = x,
                v1 = y,
                v2 = z
            };

            return mat.Translate(translationVector, localAxis);
        }
        
        #endregion

        #region Rotation

        private static HmdMatrix34_t RotationX(double angle, bool degrees = true)
        {
            if (degrees) angle = (Math.PI * angle / 180.0);
            return new HmdMatrix34_t
            {
                m0 = 1,
                m5 = (float)Math.Cos(angle),
                m6 = (float)-Math.Sin(angle),
                m9 = (float)Math.Sin(angle),
                m10 = (float)Math.Cos(angle),
            };
        }
        private static HmdMatrix34_t RotationY(double angle, bool degrees = true)
        {
            if (degrees) angle = (Math.PI * angle / 180.0);
            return new HmdMatrix34_t
            {
                m0 = (float)Math.Cos(angle),
                m2 = (float)Math.Sin(angle),
                m5 = 1,
                m8 = (float)-Math.Sin(angle),
                m10 = (float)Math.Cos(angle),
            };
        }
        private static HmdMatrix34_t RotationZ(double angle, bool degrees = true)
        {
            if (degrees) angle = (Math.PI * angle / 180.0);
            return new HmdMatrix34_t
            {
                m0 = (float)Math.Cos(angle),
                m1 = (float)-Math.Sin(angle),
                m4 = (float)Math.Sin(angle),
                m5 = (float)Math.Cos(angle),
                m10 = 1,
            };
        }

        public static HmdMatrix34_t RotateX(this HmdMatrix34_t mat, double angle, bool degrees = true)
        {
            return mat.Multiply(RotationX(angle, degrees));
        }
        public static HmdMatrix34_t RotateY(this HmdMatrix34_t mat, double angle, bool degrees = true)
        {
            return mat.Multiply(RotationY(angle, degrees));
        }
        public static HmdMatrix34_t RotateZ(this HmdMatrix34_t mat, double angle, bool degrees = true)
        {
            return mat.Multiply(RotationZ(angle, degrees));
        }

        public static HmdMatrix34_t Rotate(this HmdMatrix34_t mat, double angleX, double angleY, double angleZ, bool degrees = true)
        {
            // HmdMatrix34_t rotation = mat.RotateX(angleX, degrees);
            // rotation = rotation.RotateY(angleY, degrees);
            // rotation = rotation.RotateZ(angleZ, degrees);
            return mat.RotateX(angleX, degrees).RotateY(angleY, degrees).RotateZ(angleZ, degrees);
        }

        #endregion

        #region Multiplication

        public static HmdMatrix34_t Multiply(this HmdMatrix34_t matA, HmdMatrix34_t matB) =>
            EasyOpenVRSingleton.Utils.MultiplyMatrixWithMatrix(matA, matB);

        public static HmdMatrix34_t Multiply(this HmdMatrix34_t mat, float val)
        {
            return new HmdMatrix34_t
            {
                m0 = mat.m0 * val, m1 = mat.m1 * val, m2 = mat.m2 * val, m3 = mat.m3 * val,
                m4 = mat.m4 * val, m5 = mat.m5 * val, m6 = mat.m6 * val, m7 = mat.m7 * val,
                m8 = mat.m8 * val, m9 = mat.m9 * val, m10 = mat.m10 * val, m11 = mat.m11 * val
            };
        }

        #endregion

        #region Addition

        public static HmdMatrix34_t Add(this HmdMatrix34_t matA, HmdMatrix34_t matB)
        {
            return new HmdMatrix34_t
            {
                m0 = matA.m0 + matB.m0, m1 = matA.m1 + matB.m1, m2 = matA.m2 + matB.m2, m3 = matA.m3 + matB.m3,
                m4 = matA.m4 + matB.m4, m5 = matA.m5 + matB.m5, m6 = matA.m6 + matB.m6, m7 = matA.m7 + matB.m7,
                m8 = matA.m8 + matB.m8, m9 = matA.m9 + matB.m9, m10 = matA.m10 + matB.m10, m11 = matA.m11 + matB.m11,
            };
        }

        public static HmdMatrix34_t Add(this HmdMatrix34_t mat, HmdVector3_t vec)
        {
            mat.m3 += vec.v0;
            mat.m7 += vec.v1;
            mat.m11 += vec.v2;
            return mat;
        }

        #endregion

        #region Subtraction

        public static HmdMatrix34_t Subtract(this HmdMatrix34_t matA, HmdMatrix34_t matB)
        {
            return new HmdMatrix34_t
            {
                m0 = matA.m0 - matB.m0, m1 = matA.m1 - matB.m1, m2 = matA.m2 - matB.m2, m3 = matA.m3 - matB.m3,
                m4 = matA.m4 - matB.m4, m5 = matA.m5 - matB.m5, m6 = matA.m6 - matB.m6, m7 = matA.m7 - matB.m7,
                m8 = matA.m8 - matB.m8, m9 = matA.m9 - matB.m9, m10 = matA.m10 - matB.m10, m11 = matA.m11 - matB.m11,
            };
        }

        public static HmdMatrix34_t Subtract(this HmdMatrix34_t mat, HmdVector3_t vec)
        {
            mat.m3 -= vec.v0;
            mat.m7 -= vec.v1;
            mat.m11 -= vec.v2;
            return mat;
        }

        #endregion

        #region Interpolation

        public static HmdMatrix34_t Lerp(this HmdMatrix34_t matA, HmdMatrix34_t matB, float amount)
        {
            return new HmdMatrix34_t
            {
                // Row one
                m0 = matA.m0 + (matB.m0 - matA.m0) * amount,
                m1 = matA.m1 + (matB.m1 - matA.m1) * amount,
                m2 = matA.m2 + (matB.m2 - matA.m2) * amount,
                m3 = matA.m3 + (matB.m3 - matA.m3) * amount,
                
                // Row two
                m4 = matA.m4 + (matB.m4 - matA.m4) * amount,
                m5 = matA.m5 + (matB.m5 - matA.m5) * amount,
                m6 = matA.m6 + (matB.m6 - matA.m6) * amount,
                m7 = matA.m7 + (matB.m7 - matA.m7) * amount,
                
                // Row three
                m8 = matA.m8 + (matB.m8 - matA.m8) * amount,
                m9 = matA.m9 + (matB.m9 - matA.m9) * amount,
                m10 = matA.m10 + (matB.m10 - matA.m10) * amount,
                m11 = matA.m11 + (matB.m11 - matA.m11) * amount,
            };
        }

        #endregion

        #region Transformation

        public static HmdVector3_t EulerAngles(this HmdMatrix34_t mat)
        {
            double yaw = Math.Atan2(mat.m2, mat.m10);
            double pitch = -Math.Asin(mat.m6);
            double roll = Math.Atan2(mat.m4, mat.m5);

            return new HmdVector3_t
            {
                v1 = (float) yaw,
                v0 = (float) pitch,
                v2 = (float) roll
            };
        }

        public static HmdMatrix34_t FromEuler(this HmdMatrix34_t mat, HmdVector3_t angles)
        {
            HmdMatrix34_t Rx = RotationX(angles.v0, false);
            HmdMatrix34_t Ry = RotationY(angles.v1, false);
            HmdMatrix34_t Rz = RotationZ(angles.v2, false);

            HmdMatrix34_t rotation = Ry.Multiply(Rx).Multiply(Rz);

            mat.m0 = rotation.m0;
            mat.m1 = rotation.m1;
            mat.m2 = rotation.m2;
            mat.m4 = rotation.m4;
            mat.m5 = rotation.m5;
            mat.m6 = rotation.m6;
            mat.m8 = rotation.m8;
            mat.m9 = rotation.m9;
            mat.m10 = rotation.m10;

            return mat;
        }

        #endregion

        #region Acquisition
        public static HmdVector3_t GetPosition(this HmdMatrix34_t mat)
        {
            return new HmdVector3_t
            {
                v1 = mat.m3,
                v0 = mat.m7,
                v2 = mat.m11
            };
        }
        #endregion
    }
}
