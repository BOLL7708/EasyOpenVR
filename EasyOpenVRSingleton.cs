using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using Valve.VR;

namespace BOLL7708
{
    public sealed class EasyOpenVRSingleton
    {
        private static EasyOpenVRSingleton __instance = null;
        private EasyOpenVRSingleton() { }

        public static EasyOpenVRSingleton Instance
        {
            get
            {
                if (__instance == null) __instance = new EasyOpenVRSingleton();
                return __instance;
            }
        }
        #region setup
        private EVRApplicationType _appType = EVRApplicationType.VRApplication_Background;
        public void SetApplicationType(EVRApplicationType appType)
        {
            _appType = appType;
        }

        private bool _debug = false;
        public void SetDebug(bool debug)
        {
            _debug = debug;
        }
        #endregion

        #region init
        private uint _initState = 0;
        public bool Init()
        {
            EVRInitError error = EVRInitError.None;
            _initState = OpenVR.InitInternal(ref error, _appType);
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
            OpenVR.Compositor.GetCumulativeStats(ref stats, (uint)System.Runtime.InteropServices.Marshal.SizeOf(stats));
            return stats;
        }

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
        #endregion

        #region controller
        /*
         * Includes things like analogue axes of triggers, pads & sticks
         */
        public VRControllerState_t GetControllerState(uint index)
        {
            VRControllerState_t state = new VRControllerState_t();
            var success = OpenVR.System.GetControllerState(index, ref state, (uint) System.Runtime.InteropServices.Marshal.SizeOf(state));
            if (_debug && !success) Debug.WriteLine("Failure getting ControllerState");
            return state;
        }
        #endregion

        #region tracked_device
        /*
         * Example of property: ETrackedDeviceProperty.Prop_DeviceBatteryPercentage_Float
         */
        public float GetFloatTrackedDeviceProperty(uint index, ETrackedDeviceProperty property)
        {
            ETrackedPropertyError error = new ETrackedPropertyError();
            var success = error == ETrackedPropertyError.TrackedProp_Success;
            if (_debug && !success) Debug.WriteLine("OpenVR Float Prop Error: " + error.ToString());
            return OpenVR.System.GetFloatTrackedDeviceProperty(index, property, ref error);
        }
        /*
         * Example of property: ETrackedDeviceProperty.Prop_SerialNumber_String
         */
        public string GetStringTrackedDeviceProperty(uint index, ETrackedDeviceProperty property)
        {
            ETrackedPropertyError error = new ETrackedPropertyError();
            StringBuilder sb = new StringBuilder();
            OpenVR.System.GetStringTrackedDeviceProperty(index, ETrackedDeviceProperty.Prop_SerialNumber_String, sb, OpenVR.k_unMaxPropertyStringSize, ref error);
            var success = error == ETrackedPropertyError.TrackedProp_Success;
            if (_debug && !success) Debug.WriteLine("OpenVR String Prop Error: " + error.ToString());
            return sb.ToString();
        }
        #endregion

        #region events
        public VREvent_t[] GetNewEvents()
        {
            var vrEvents = new List<VREvent_t>();
            var vrEvent = new VREvent_t();
            uint eventSize = (uint)System.Runtime.InteropServices.Marshal.SizeOf(vrEvent);
            while (OpenVR.System.PollNextEvent(ref vrEvent, eventSize))
            {
                vrEvents.Add(vrEvent);
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
            uint eventSize = (uint)System.Runtime.InteropServices.Marshal.SizeOf(vrEvent);
            while (OpenVR.Overlay.PollNextOverlayEvent(overlayHandle, ref vrEvent, eventSize))
            {
                vrEvents.Add(vrEvent);
            }
            return vrEvents.ToArray();
        }
        #endregion
    }
}
