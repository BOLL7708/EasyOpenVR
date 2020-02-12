using System;
using System.Collections.Generic;
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
            OpenVR.Compositor.GetCumulativeStats(ref stats, (uint) Marshal.SizeOf(stats));
            return stats;
        }

        public Compositor_FrameTiming GetFrameTiming()
        {
            Compositor_FrameTiming timing = new Compositor_FrameTiming();
            timing.m_nSize = (uint) Marshal.SizeOf(timing);
            var success = OpenVR.Compositor.GetFrameTiming(ref timing, 0);
            if (!success) DebugLog("Could not get frame timing.");
            return timing;
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
                // This at it appears the bottom gets reset to 0 at a regular interval anyway.
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
         */
        public VRControllerState_t GetControllerState(uint index)
        {
            VRControllerState_t state = new VRControllerState_t();
            var success = OpenVR.System.GetControllerState(index, ref state, (uint) Marshal.SizeOf(state));
            if (!success) DebugLog("Failure getting ControllerState");
            return state;
        }

        /**
         * Will return the index of the role if found
         * Useful if you want to know which controller is right or left.
         * Note: Seems redundant now, but wasn't in the past, I think.
         */
        public uint GetIndexForControllerRole(ETrackedControllerRole role)
        {
            return OpenVR.System.GetTrackedDeviceIndexForControllerRole(role);
        }
        #endregion

        #region tracked_device
        /*
         * Example of property: ETrackedDeviceProperty.Prop_DeviceBatteryPercentage_Float
         */
        public float GetFloatTrackedDeviceProperty(uint index, ETrackedDeviceProperty property)
        {
            var error = new ETrackedPropertyError();
            var result = OpenVR.System.GetFloatTrackedDeviceProperty(index, property, ref error);
            DebugLog(error);
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
         * Example of property: ETrackedDeviceProperty.Prop_ContainsProximitySensor_Bool
         */
        public bool GetBooleanTrackedDeviceProperty(uint index, ETrackedDeviceProperty property)
        {
            var error = new ETrackedPropertyError();
            var result = OpenVR.System.GetBoolTrackedDeviceProperty(index, property, ref error);
            DebugLog(error);
            return result;
        }

        public void TriggerHapticPulseInController(ETrackedControllerRole role)
        {
            var index = GetIndexForControllerRole(ETrackedControllerRole.LeftHand);
            OpenVR.System.TriggerHapticPulse(index, 0, 10000); // This works: https://github.com/ValveSoftware/openvr/wiki/IVRSystem::TriggerHapticPulse
        }
        #endregion

        #region events
        public VREvent_t[] GetNewEvents()
        {
            var vrEvents = new List<VREvent_t>();
            var vrEvent = new VREvent_t();
            uint eventSize = (uint) Marshal.SizeOf(vrEvent);
            try
            {
                while (OpenVR.System.PollNextEvent(ref vrEvent, eventSize))
                {
                    vrEvents.Add(vrEvent);
                }
            } catch(Exception e)
            {
                if (_debug) Debug.WriteLine($"Could not get new events: {e.StackTrace}");
            }
            
            return vrEvents.ToArray();
        }

        /*
         * Example of overlayHandle: OpenVR.Overlay.GetGamepadFocusOverlay() for example.
         */
        public VREvent_t[] GetNewOverlayEvents(ulong overlayHandle)
        {
            var vrEvents = new List<VREvent_t>();
            var vrEvent = new VREvent_t();
            uint eventSize = (uint) Marshal.SizeOf(vrEvent);
            while (OpenVR.Overlay.PollNextOverlayEvent(overlayHandle, ref vrEvent, eventSize))
            {
                vrEvents.Add(vrEvent);
            }
            return vrEvents.ToArray();
        }
        #endregion

        #region input
        public enum InputType
        {
            Analog,
            Digital
        }
        private class InputAction
        {
            public ulong handle;
            public object data;
            public InputType type;
            public object action;
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

        private EVRInputError RegisterAction(string path, ref InputAction ia)
        {
            ulong handle = 0;
            var error = OpenVR.Input.GetActionHandle(path, ref handle);
            if (handle != 0 && error == EVRInputError.None)
            {
                ia.handle = handle;
                _inputActions.Add(ia);
            }
            else DebugLog(error);
            return error;
        }

        /**
         * Register an analog action with a callback action
         */
        public bool RegisterAnalogAction(string path, Action<InputAnalogActionData_t, ulong> action)
        {
            var ia = new InputAction
            {
                type = InputType.Analog,
                action = action,
                data = new InputAnalogActionData_t()
            };
            var error = RegisterAction(path, ref ia);
            return DebugLog(error);
        }

        /**
         * Register a digital action with a callback action
         */
        public bool RegisterDigitalAction(string path, Action<InputDigitalActionData_t, ulong> action)
        {
            var inputAction = new InputAction
            {
                type = InputType.Digital,
                action = action,
                data = new InputDigitalActionData_t()
            };
            var error = RegisterAction(path, ref inputAction);
            return DebugLog(error);
        }

        /**
         * Retrieve the handle for the input source of a specific input device
         */
        public ulong GetInputSourceHandle(string path)
        {
            ulong handle = 0;
            var error = OpenVR.Input.GetInputSourceHandle(path, ref handle);
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
            if (inputSourceHandles == null) inputSourceHandles = new ulong[] { OpenVR.k_ulInvalidInputValueHandle };
            var error = OpenVR.Input.UpdateActionState(_inputActionSets.ToArray(), (uint)Marshal.SizeOf(typeof(VRActiveActionSet_t)));
 
            _inputActions.ForEach((InputAction action) =>
            {
                switch(action.type)
                {
                    case InputType.Analog:
                        foreach(var handle in inputSourceHandles) GetAnalogAction(action, handle);
                        break;
                    case InputType.Digital:
                        foreach (var handle in inputSourceHandles) GetDigitalAction(action, handle);
                        break;
                }
            });
            return DebugLog(error);
        }

        private bool GetAnalogAction(InputAction inputAction, ulong inputSourceHandle)
        {
            var size = (uint)Marshal.SizeOf(typeof(InputAnalogActionData_t));
            var data = (InputAnalogActionData_t)inputAction.data;
            var error = OpenVR.Input.GetAnalogActionData(inputAction.handle, ref data, size, inputSourceHandle);
            var action = ((Action<InputAnalogActionData_t, ulong>)inputAction.action);
            if (data.bActive) action.Invoke(data, inputSourceHandle);
            return DebugLog(error, $"handle: {inputAction.handle}, error");
        }

        private bool GetDigitalAction(InputAction inputAction, ulong inputSourceHandle)
        {
            var size = (uint)Marshal.SizeOf(typeof(InputDigitalActionData_t));
            var data = (InputDigitalActionData_t) inputAction.data;
            var error = OpenVR.Input.GetDigitalActionData(inputAction.handle, ref data, size, inputSourceHandle);
            var action = ((Action<InputDigitalActionData_t, ulong>)inputAction.action);
            if (data.bActive && data.bChanged) action.Invoke(data, inputSourceHandle);
            return DebugLog(error, $"handle: {inputAction.handle}, error");
        }
        #endregion

        #region screenshots

        /*
         * When used the files should end up in %programfiles(x86)%\Steam\steamapps\common\SteamVR\bin\
         */
        public bool TakeScreenshot()
        {
            uint handle = 0;
            var timestamp = DateTime.Now.ToString("yyyyMMddHHmmss_ffff");

            // Stereo is default and supported by every scene application, will not show notification.
            // This also means you have to be running a scene application for a screenshot to be saved.
            var error = OpenVR.Screenshots.TakeStereoScreenshot(ref handle, timestamp, $"{timestamp}_vr");
            return DebugLog(error);
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
            var error = EVROverlayError.None;
            ulong handle = 0;
            var key = Guid.NewGuid().ToString();
            error = OpenVR.Overlay.CreateOverlay(key, notificationTitle, ref handle);
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
            if(error == EVRNotificationError.OK) _notifications.Remove(id);
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
        private bool DebugLog(Enum e, string message = "error")
        {
            var errorVal = Convert.ChangeType(e, e.GetTypeCode());
            var ok = (int)errorVal == 0;
            if (_debug)
            {
                var st = new StackTrace();
                var sf = st.GetFrame(1);
                var methodName = sf.GetMethod().Name;
                var text = $"{methodName} {message}: {Enum.GetName(e.GetType(), e)}";
                if (!ok)
                {
                    _debugLogAction?.Invoke(text);
                    Debug.WriteLine(text);
                }
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

        public static class Utils
        {
            public static HmdVector3_t InvertVector(HmdVector3_t position)
            {
                position.v0 = -position.v0;
                position.v1 = -position.v1;
                position.v2 = -position.v2;
                return position;
            }

            public static HmdVector3_t MultiplyVectorWithRotationMatrix(HmdVector3_t v, HmdMatrix34_t m)
            {
                var newVector = new HmdVector3_t();
                newVector.v0 = m.m0 * v.v0 + m.m1 * v.v1 + m.m2 * v.v2;
                newVector.v1 = m.m4 * v.v0 + m.m5 * v.v1 + m.m6 * v.v2;
                newVector.v2 = m.m8 * v.v0 + m.m9 * v.v1 + m.m10 * v.v2;
                return newVector;
            }
        }

        public static class BitmapUtils
        {
            public static NotificationBitmap_t NotificationBitmapFromBitmap(Bitmap bmp)
            {
                return NotificationBitmapFromBitmapData(BitmapDataFromBitmap(bmp));
            }

            public static BitmapData BitmapDataFromBitmap(Bitmap bmpIn)
            {
                Bitmap bmp = (Bitmap)bmpIn.Clone();
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
}
