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
                if (_debug) Debug.WriteLine($"You might be building for 32bit with a 64bit .dll, error: {e.Message}");
            }
            var success = error == EVRInitError.None;
            if (_debug && !success) Debug.WriteLine("OpenVR Init Error: " + error.ToString());
            return success && _initState > 0;
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
            if(_debug && !success)
            {
                if (_debug) Debug.WriteLine("Could not get frame timing.");
            }
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
            if (_debug && !success) Debug.WriteLine("Failure getting PlayAreaRect");
            return rect;
        }

        public HmdVector2_t GetPlayAreaSize()
        {
            var size = new HmdVector2_t();
            var success = OpenVR.Chaperone.GetPlayAreaSize(ref size.v0, ref size.v1);
            if (_debug && !success) Debug.WriteLine("Failure getting PlayAreaSize");
            return size;
        }

        public void MoveUniverse(HmdVector3_t offset, bool moveChaperone = true, bool moveLiveZeroPose = true)
        {
            OpenVR.ChaperoneSetup.RevertWorkingCopy(); // Sets working copy to current live settings
            if (moveLiveZeroPose) MoveLiveZeroPose(offset);
            if (moveChaperone) MoveChaperoneBounds(Utils.InvertVector(offset));
            OpenVR.ChaperoneSetup.CommitWorkingCopy(EChaperoneConfigFile.Live); // Apply changes to live settings
        }

        public void MoveChaperoneBounds(HmdVector3_t offset)
        {
            HmdQuad_t[] physQuad;
            OpenVR.ChaperoneSetup.GetWorkingCollisionBoundsInfo(out physQuad);

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
            if (_debug && !success) Debug.WriteLine("Failure getting ControllerState");
            return state;
        }

        /**
         * Will return the index of the role if found, otherwise uint.MaxValue
         * Useful if you want to know which controller is right or left.
         */
        public uint GetIndexForControllerRole(ETrackedControllerRole role)
        {
            for (uint i = 0; i < OpenVR.k_unMaxTrackedDeviceCount; i++)
            {
                var r = OpenVR.System.GetControllerRoleForTrackedDeviceIndex(i);
                if (r == role) return i;
            }
            return uint.MaxValue;
        }
        #endregion

        #region tracked_device
        /*
         * Example of property: ETrackedDeviceProperty.Prop_DeviceBatteryPercentage_Float
         */
        public float GetFloatTrackedDeviceProperty(uint index, ETrackedDeviceProperty property)
        {
            var error = new ETrackedPropertyError();
            var result = 0f;
            try
            {
                result = OpenVR.System.GetFloatTrackedDeviceProperty(index, property, ref error);
            }
            catch (Exception e)
            {
                if (_debug) Debug.WriteLine($"OpenVR Float Prop Exception: {e.Message}");
            }
            var success = error == ETrackedPropertyError.TrackedProp_Success;
            if (_debug && !success) Debug.WriteLine("OpenVR Float Prop Error: " + error.ToString());
            return result;
        }
        /*
         * Example of property: ETrackedDeviceProperty.Prop_SerialNumber_String
         */
        public string GetStringTrackedDeviceProperty(uint index, ETrackedDeviceProperty property)
        {
            var error = new ETrackedPropertyError();
            StringBuilder sb = new StringBuilder();
            try
            {
                OpenVR.System.GetStringTrackedDeviceProperty(index, property, sb, OpenVR.k_unMaxPropertyStringSize, ref error);
            }
            catch (Exception e)
            {
                if (_debug) Debug.WriteLine($"OpenVR String Prop Exception: {e.Message}");
            }
            var success = error == ETrackedPropertyError.TrackedProp_Success;
            if (_debug && !success) Debug.WriteLine("OpenVR String Prop Error: " + error.ToString());
            return sb.ToString();
        }


        /*
         * Example of property: ETrackedDeviceProperty.Prop_EdidProductID_Int32
         */
        public int GetIntegerTrackedDeviceProperty(uint index, ETrackedDeviceProperty property)
        {
            var error = new ETrackedPropertyError();
            var result = 0;
            try
            {
                result = OpenVR.System.GetInt32TrackedDeviceProperty(index, property, ref error);
            }
            catch (Exception e)
            {
                if (_debug) Debug.WriteLine($"OpenVR Integer Prop Exception: {e.Message}");
            }
            var success = error == ETrackedPropertyError.TrackedProp_Success;
            if (_debug && !success) Debug.WriteLine("OpenVR Integer Prop Error: " + error.ToString());
            return result;
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

        public EVRInputError RegisterActionSet(string path)
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
            } else
            {
                if (_debug) Debug.WriteLine($"Could not register action set: {Enum.GetName(typeof(EVRInputError), error)}");
            }
            return error;
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
            else if (_debug) Debug.WriteLine($"Could not register action: {Enum.GetName(typeof(EVRInputError), error)}");
            return error;
        }

        /**
         * Register an analog action with a callback action
         */
        public EVRInputError RegisterAnalogAction(string path, Action<float, float, float> action)
        {
            var ia = new InputAction
            {
                type = InputType.Analog,
                action = action,
                data = new InputAnalogActionData_t()
            };
            return RegisterAction(path, ref ia);
        }

        /**
         * Register a digital action with a callback action
         */
        public EVRInputError RegisterDigitalAction(string path, Action<bool> action)
        {
            var inputAction = new InputAction
            {
                type = InputType.Digital,
                action = action,
                data = new InputDigitalActionData_t()
            };
            return RegisterAction(path, ref inputAction);
        }

        /**
         * Update all action states, this will trigger stored actions if needed.
         * Digital actions triggers on change, analog actions every update.
         */
        public void UpdateActionStates()
        {
            var error = OpenVR.Input.UpdateActionState(_inputActionSets.ToArray(), (uint)Marshal.SizeOf(typeof(VRActiveActionSet_t)));
            if (_debug && error != EVRInputError.None) Debug.WriteLine($"UpdateActionState Error: {Enum.GetName(typeof(EVRInputError), error)}");

            _inputActions.ForEach((InputAction action) =>
            {
                switch(action.type)
                {
                    case InputType.Analog:
                        GetAnalogAction(action);
                        break;
                    case InputType.Digital:
                        GetDigitalAction(action);
                        break;
                }
            });
        }

        private void GetAnalogAction(InputAction inputAction)
        {
            var size = (uint)Marshal.SizeOf(typeof(InputAnalogActionData_t));
            var data = (InputAnalogActionData_t)inputAction.data;
            var error = OpenVR.Input.GetAnalogActionData(inputAction.handle, ref data, size, 0);
            if (_debug && error != EVRInputError.None) Debug.WriteLine($"AnalogActionDataError: {Enum.GetName(typeof(EVRInputError), error)}, handle: {inputAction.handle}");
            var action = ((Action<float, float, float>)inputAction.action);
            if (data.bActive) action.Invoke(data.x, data.y, data.z);
        }

        private void GetDigitalAction(InputAction inputAction)
        {
            var size = (uint)Marshal.SizeOf(typeof(InputDigitalActionData_t));
            var data = (InputDigitalActionData_t) inputAction.data;
            var error = OpenVR.Input.GetDigitalActionData(inputAction.handle, ref data, size, 0);
            if(_debug && error != EVRInputError.None) Debug.WriteLine($"DigitalActionDataError: {Enum.GetName(typeof(EVRInputError), error)}, handle: {inputAction.handle}");
            var action = ((Action<bool>)inputAction.action);
            if (data.bActive && data.bChanged) action.Invoke(data.bState);
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
            if (error != EVRScreenshotError.None)
            {
                if(_debug) Debug.WriteLine("Screenshot error: " + Enum.GetName(typeof(EVRScreenshotError), error));
            }
            return error == EVRScreenshotError.None;
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
            if (error == EVROverlayError.None) return handle;
            else if (_debug) Debug.WriteLine($"Notification overlay error: {Enum.GetName(typeof(EVROverlayError), error)}");
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
            while (id == 0 || _notifications.Contains(id)) id = (uint)_rnd.Next();
            var error = OpenVR.Notifications.CreateNotification(overlayHandle, 0, type, message, style, ref bitmap, ref id);
            if (_debug && error != EVRNotificationError.OK) Debug.WriteLine($"Show notification error: {Enum.GetName(typeof(EVRNotificationError), error)}");
            _notifications.Add(id);
            return id;
        }

        /*
         * Used to dismiss a persistent notification.
         */
        public void DismissNotification(uint id)
        {
            var error = OpenVR.Notifications.RemoveNotification(id);
            if (_debug && error != EVRNotificationError.OK) Debug.WriteLine($"Hide notification error: {Enum.GetName(typeof(EVRNotificationError), error)}");
            else _notifications.Remove(id);
        }

        public void EmptyNotificationsQueue()
        {
            var error = EVRNotificationError.OK;
            foreach (uint id in _notifications)
            {
                error = OpenVR.Notifications.RemoveNotification(id);
                if (_debug && error != EVRNotificationError.OK) Debug.WriteLine($"Clear notifications error: {Enum.GetName(typeof(EVRNotificationError), error)}");
            }
            _notifications.Clear();
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
        public EVRApplicationError LoadAppManifest(string relativePath)
        {
            var error = OpenVR.Applications.AddApplicationManifest(Path.GetFullPath(relativePath), false);
            if (_debug && error != EVRApplicationError.None) Debug.WriteLine($"Failed to load Application Manifest: {Enum.GetName(typeof(EVRApplicationError), error)}");
            return error;
        }

        public String GetRuntimeVersion()
        {
            var version = "";
            if(OpenVR.IsRuntimeInstalled())
            {
                try
                {
                    String path = OpenVR.RuntimePath() + "bin\\version.txt";
                    if(File.Exists(path)) version = File.ReadAllText(path).Trim();
                } catch(Exception e)
                {
                    if(_debug) Debug.WriteLine("Error reading runtime version: " + e.Message);
                }
            }
            return version;
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
