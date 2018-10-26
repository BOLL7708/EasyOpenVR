using System;
using System.Collections.Generic;
using System.Diagnostics;
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

        private bool _debug = false;
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
                Debug.WriteLine("Could not get frame timing.");
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
            var result = OpenVR.System.GetFloatTrackedDeviceProperty(index, property, ref error);
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
            OpenVR.System.GetStringTrackedDeviceProperty(index, property, sb, OpenVR.k_unMaxPropertyStringSize, ref error);
            var success = error == ETrackedPropertyError.TrackedProp_Success;
            if (_debug && !success) Debug.WriteLine("OpenVR String Prop Error: " + error.ToString());
            return sb.ToString();
        }
        #endregion

        /*
         * Example of property: ETrackedDeviceProperty.Prop_EdidProductID_Int32
         */
        public int GetIntegerTrackedDeviceProperty(uint index, ETrackedDeviceProperty property)
        {
            var error = new ETrackedPropertyError();
            var result = OpenVR.System.GetInt32TrackedDeviceProperty(index, property, ref error);
            var success = error == ETrackedPropertyError.TrackedProp_Success;
            if (_debug && !success) Debug.WriteLine("OpenVR Integer Prop Error: " + error.ToString());
            return result;
        }

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
                Debug.WriteLine($"Could not get new events: {e.StackTrace}");
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
        public String GetRuntimeVersion()
        {
            var version = "";
            if(OpenVR.IsRuntimeInstalled())
            {
                String path = OpenVR.RuntimePath() + "bin\\version.txt";
                Debug.WriteLine(path);
                if(File.Exists(path)) version = File.ReadAllText(path);
                Debug.WriteLine(version);
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
