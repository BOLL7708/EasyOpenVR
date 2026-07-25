using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using Valve.VR;

namespace EasyOpenVR;

public partial class EasyOpenVr
{
    public class InputMethods(EasyOpenVr evr)
    {
        /**
         * Based on the SteamVR Unity Plugin: https://github.com/ValveSoftware/steamvr_unity_plugin/blob/master/Assets/SteamVR/Input/SteamVR_Input_Sources.cs
         * But we have matched the string values to actual constants in OpenVR where available.
         * Is used to get the handle for any specific input source.
         */
        public enum InputSource
        {
            #region General
            [Description("/unrestricted")] 
            Any,
            #endregion
            
            // Reordered in the order they appear on the body from top to bottom for each side.
            #region Left
            [Description(OpenVR.k_pchPathUserShoulderLeft)]
            LeftShoulder,

            [Description(OpenVR.k_pchPathUserElbowLeft)]
            LeftElbow,
            
            [Description(OpenVR.k_pchPathUserWristLeft)]
            LeftWrist,

            [Description(OpenVR.k_pchPathUserHandLeft)]
            LeftHand,

            [Description(OpenVR.k_pchPathUserKneeLeft)]
            LeftKnee,

            [Description(OpenVR.k_pchPathUserAnkleLeft)]
            LeftAnkle,

            [Description(OpenVR.k_pchPathUserFootLeft)]
            LeftFoot,
            #endregion
            #region Right
            [Description(OpenVR.k_pchPathUserHandRight)]
            RightHand,

            [Description(OpenVR.k_pchPathUserWristRight)]
            RightWrist,

            [Description(OpenVR.k_pchPathUserElbowRight)]
            RightElbow,

            [Description(OpenVR.k_pchPathUserShoulderRight)]
            RightShoulder,

            [Description(OpenVR.k_pchPathUserKneeRight)]
            RightKnee,

            [Description(OpenVR.k_pchPathUserAnkleRight)]
            RightAnkle,

            [Description(OpenVR.k_pchPathUserFootRight)]
            RightFoot,
            #endregion
            
            #region Center
            [Description(OpenVR.k_pchPathUserHead)]
            Head,

            [Description(OpenVR.k_pchPathUserChest)]
            Chest,

            [Description(OpenVR.k_pchPathUserWaist)]
            Waist,
            #endregion
            
            #region Devices
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
            #endregion
        }

        public enum InputType
        {
            Analog,
            Digital,
            Pose,
            SkeletonSummary
        }

        internal class InputAction
        {
            internal string path;
            internal object data;
            internal InputType type;
            internal object action;
            internal ulong handle = 0;
            internal string pathEnd = "";

            internal bool
                isChord = false; // Needed to avoid filtering on the input source handle as Chords can flip their on/off action between sources depending on which button is activated/deactivated first.

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

        internal List<InputAction> _inputActions = [];
        private readonly List<VRActiveActionSet_t> _inputActionSets = [];

        /**
         * Load the actions manifest to register actions for the application
         * OBS: Make sure the encoding is UTF8 and not UTF8+BOM
         */
        public EVRInputError LoadActionManifest(string relativePath)
        {
            // TODO: Validate that file exists
            return OpenVR.Input.SetActionManifestPath(Path.GetFullPath(relativePath));
        }

        public EasyOpenVrResult RegisterActionSet(string path)
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

            return evr.DebugLog(error);
        }

        private EVRInputError RegisterAction(ref InputAction ia)
        {
            ulong handle = 0;
            var error = OpenVR.Input.GetActionHandle(ia.path, ref handle);
            var pathParts = ia.path.Split('/');
            if (handle != 0 && error == EVRInputError.None)
            {
                ia.handle = handle;
                ia.pathEnd = pathParts[^1];
                _inputActions.Add(ia);
            }
            else evr.DebugLog(error);

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
        public EasyOpenVrResult RegisterAnalogAction(string path, Action<InputAnalogActionData_t, InputActionInfo> action,
            bool isChord = false)
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
            return evr.DebugLog(error);
        }

        /**
         * Register a skeleton action with a callback action
         */
        public EasyOpenVrResult RegisterSkeletonSummaryAction(string path, Action<VRSkeletalSummaryData_t, InputActionInfo> action)
        {
            var ia = new InputAction
            {
                path = path,
                type = InputType.SkeletonSummary,
                action = action,
                data = new VRSkeletalSummaryData_t()
            };
            var error = RegisterAction(ref ia);
            return evr.DebugLog(error);
        }

        /**
         * Register a digital action with a callback action
         */
        public EasyOpenVrResult RegisterDigitalAction(string path, Action<InputDigitalActionData_t, InputActionInfo> action,
            bool isChord = false)
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
            return evr.DebugLog(error);
        }

        /**
         * Register a digital action with a callback action
         */
        public EasyOpenVrResult RegisterPoseAction(string path, Action<InputPoseActionData_t, InputActionInfo> action,
            bool isChord = false)
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
            return evr.DebugLog(error);
        }

        /**
         * Retrieve the handle for the input source of a specific input device
         */
        public ulong GetInputSourceHandle(InputSource inputSource)
        {
            var attributes = (DescriptionAttribute[]) (inputSource
                .GetType()
                .GetField(inputSource.ToString())
                ?.GetCustomAttributes(typeof(DescriptionAttribute), false) ?? []);
            var source = attributes.Length > 0 ? attributes[0].Description : string.Empty;

            ulong handle = 0;
            var error = OpenVR.Input.GetInputSourceHandle(source, ref handle);
            evr.DebugLog(error);
            return handle;
        }


        /// <summary>
        /// Update all action states, this will trigger stored actions if needed.
        /// <para>Digital actions triggers on change, analog actions every update.</para>
        /// </summary>
        /// <b>OBS</b>: Only run this once per update, or you'll get no input data at all.
        /// <returns></returns>
        public EasyOpenVrResult UpdateActionStates(ulong[] inputSourceHandles, ulong skeletonSummaryInputSourceHandle)
        {
            if (inputSourceHandles.Length == 0) inputSourceHandles = [OpenVR.k_ulInvalidPathHandle];
            var error = OpenVR.Input.UpdateActionState(
                [.. _inputActionSets],
                (uint)Marshal.SizeOf<VRActiveActionSet_t>()
            );
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
                    case InputType.SkeletonSummary:
                        GetSkeletalSummary(action, skeletonSummaryInputSourceHandle);
                        break;
                }
            });
            return evr.DebugLog(error);
        }

        private bool GetSkeletalSummary(InputAction inputAction, ulong inputSourceHandle)
        {
            var data = (VRSkeletalSummaryData_t)inputAction.data;
            var error = OpenVR.Input.GetSkeletalSummaryData(inputAction.handle, EVRSummaryType.FromDevice, ref data);
            var action = ((Action<VRSkeletalSummaryData_t, InputActionInfo>)inputAction.action);
            action.Invoke(data, inputAction.getInfo(inputSourceHandle));
            return true; // DebugLog(error, $"handle: {inputAction.handle}, error"); // TODO: This spams continuously when no controllers are connected.
        }

        private EasyOpenVrResult GetAnalogAction(InputAction inputAction, ulong inputSourceHandle)
        {
            if (inputAction.isChord) inputSourceHandle = 0;
            var size = (uint)Marshal.SizeOf<InputAnalogActionData_t>();
            var data = (InputAnalogActionData_t)inputAction.data;
            var error = OpenVR.Input.GetAnalogActionData(inputAction.handle, ref data, size, inputSourceHandle);
            var action = ((Action<InputAnalogActionData_t, InputActionInfo>)inputAction.action);
            if (data.bActive) action.Invoke(data, inputAction.getInfo(inputSourceHandle));
            return evr.DebugLog(error, $"handle: {inputAction.handle}, error");
        }

        private EasyOpenVrResult GetDigitalAction(InputAction inputAction, ulong inputSourceHandle)
        {
            if (inputAction.isChord) inputSourceHandle = 0;
            var size = (uint)Marshal.SizeOf<InputDigitalActionData_t>();
            var data = (InputDigitalActionData_t)inputAction.data;
            var error = OpenVR.Input.GetDigitalActionData(inputAction.handle, ref data, size, inputSourceHandle);
            var action = ((Action<InputDigitalActionData_t, InputActionInfo>)inputAction.action);
            if (data.bActive && data.bChanged) action.Invoke(data, inputAction.getInfo(inputSourceHandle));
            return evr.DebugLog(error, $"handle: {inputAction.handle}, error");
        }

        private EasyOpenVrResult GetPoseAction(InputAction inputAction, ulong inputSourceHandle)
        {
            if (inputAction.isChord) inputSourceHandle = 0;
            var size = (uint)Marshal.SizeOf<InputPoseActionData_t>();
            var data = (InputPoseActionData_t)inputAction.data;
            var error = OpenVR.Input.GetPoseActionDataRelativeToNow(inputAction.handle,
                ETrackingUniverseOrigin.TrackingUniverseStanding, 0f, ref data, size, inputSourceHandle);
            var action = (Action<InputPoseActionData_t, InputActionInfo>)inputAction.action;
            if (data.bActive) action.Invoke(data, inputAction.getInfo(inputSourceHandle));
            return evr.DebugLog(error, $"handle: {inputAction.handle}, error");
        }
    }
}