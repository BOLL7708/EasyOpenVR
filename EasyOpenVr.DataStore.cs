using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Valve.VR;

namespace EasyOpenVR;

public partial class EasyOpenVr
{
    /**
     * Based on DataStore in OpenVR2WS
     */
    public class DataStore(EasyOpenVr evr)
    {
        // TODO: All of these should not be needed, no use in caching transient states as they will be emitted via handlers and delegates anyway.
        public readonly ConcurrentDictionary<ETrackedDeviceClass, HashSet<uint>> DeviceClassToTrackedDeviceIndices = new();
        public readonly ConcurrentDictionary<ulong, InputMethods.InputSource> InputSourceHandleToInputSource = new(Environment.ProcessorCount, (int)OpenVR.k_unMaxTrackedDeviceCount);
        public readonly ConcurrentDictionary<InputMethods.InputSource, ulong> InputSourceToInputHandle = new();
        // public static readonly ConcurrentDictionary<InputMethods.InputSource, ConcurrentDictionary<string, Vec3>> analogInputActionData = new();
        // public static readonly ConcurrentDictionary<InputMethods.InputSource, ConcurrentDictionary<string, OutputDataPose>> poseInputActionData = new();
        // public static readonly ConcurrentDictionary<string, OutputDataSkeletonSummary> skeletonSummaryInputActionData = new();
        public readonly ConcurrentDictionary<InputMethods.InputSource, int> InputSourceToTrackedDeviceIndex = new();
        public readonly ConcurrentDictionary<int, InputMethods.InputSource> TrackedDeviceIndexToInputSource = new();
        public readonly ConcurrentBag<ulong> InputSourceHandles = [];

        /*
         * Run this for a device index, this will register the index to the appropriate device class.
         * If no index is supplied, all existing devices will be registered.
         */
        public void UpdateDeviceClassIndices(uint index = uint.MaxValue)
        {
            if (index != uint.MaxValue)
            {
                // Only update this one index.
                var deviceClass = OpenVR.System.GetTrackedDeviceClass(index);
                SaveIndexForDeviceClass(deviceClass, index);
            }
            else
            {
                // This loop is run at init, just to find all existing devices.
                for (uint i = 0; i < OpenVR.k_unMaxTrackedDeviceCount; i++)
                {
                    var deviceClass = OpenVR.System.GetTrackedDeviceClass(i);
                    if (deviceClass != ETrackedDeviceClass.Invalid)
                    {
                        SaveIndexForDeviceClass(deviceClass, i);
                    }
                }
            }
        }

        private void SaveIndexForDeviceClass(ETrackedDeviceClass deviceClass, uint index)
        {
            if (!DeviceClassToTrackedDeviceIndices.TryGetValue(deviceClass, out var indices))
            {
                indices = [];
                DeviceClassToTrackedDeviceIndices[deviceClass] = indices;
            }

            indices.Add(index);
        }

        public void UpdateInputDeviceHandlesAndIndices()
        {
            InputSourceHandles.Clear();
            foreach (var inputSource in Enum.GetValues<InputMethods.InputSource>())
            {
                var handle = evr.Input.GetInputSourceHandle(inputSource);
                InputSourceHandles.Add(handle);
                InputSourceHandleToInputSource[handle] = inputSource;
                InputSourceToInputHandle[inputSource] = handle;
                var info = evr.Device.GetOriginTrackedDeviceInfo(handle);
                if (info.trackedDeviceIndex == uint.MaxValue) return;
                var index = (int)info.trackedDeviceIndex;
                // Only a headset gets index 0, but it's also the default N/A when loading info.
                if (inputSource != InputMethods.InputSource.Head && index == 0) index = -1;
                InputSourceToTrackedDeviceIndex[inputSource] = index;
                TrackedDeviceIndexToInputSource[index] = inputSource;
            }
        }

        public void Reset()
        {
            DeviceClassToTrackedDeviceIndices.Clear();
            InputSourceHandleToInputSource.Clear();
        }
    }
}