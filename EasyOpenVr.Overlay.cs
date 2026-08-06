using System;
using System.Collections.Generic;
using System.Drawing;
using System.Runtime.InteropServices;
using EasyOpenVR.Utils;
using Hexa.NET.StbImage;
using Valve.VR;

namespace EasyOpenVR;

public partial class EasyOpenVr
{
    public class OverlayMethods(EasyOpenVr evr)
    {
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
        public ulong CreateOverlay(string uniqueKey, string title, HmdMatrix34_t transform, float width = 1,
            uint anchor = uint.MaxValue, ETrackingUniverseOrigin origin = ETrackingUniverseOrigin.TrackingUniverseStanding)
        {
            ulong handle = 0;
            var error = OpenVR.Overlay.CreateOverlay(uniqueKey, title, ref handle);
            if (error == EVROverlayError.None)
            {
                OpenVR.Overlay.SetOverlayWidthInMeters(handle, width);
                if (anchor != uint.MaxValue)
                    OpenVR.Overlay.SetOverlayTransformTrackedDeviceRelative(handle, anchor, ref transform);
                else OpenVR.Overlay.SetOverlayTransformAbsolute(handle, origin, ref transform);
            }

            evr.DebugLog(error);
            return handle;
        }

        /// <summary>
        /// <para>Creates a dashboard overlay that will be seen in SteamVR with a button to access it.</para>
        /// <para>The default values for sizes are based on what the Steam overlay used.</para>
        /// <para>This creates an overlay that by default has mouse input and scroll support.</para>
        /// </summary>
        /// <param name="uniqueKey">A unique key for this specific overlay.</param>
        /// <param name="title">A user-friendly title that will be shown on hover.</param>
        /// <param name="mainOverlayHandle">Outputs the main overlay handle or zero if failed.</param>
        /// <param name="thumbnailOverlayHandle">Outputs the thumbnail overlay handle or zero if failed.</param>
        /// <param name="textureWidth">The texture width, this is used to set the mouse scale.</param>
        /// <param name="textureHeight">The texture height, this is used to set the mouse scale.</param>
        /// <param name="overlayWidth">The physical size of the overlay.</param>
        /// <param name="thumbnailPath">The image that will be used for the button in the dashboard.</param>
        /// <param name="thumbnailBytes">The PNG bytes for the image to be used for the dashboard button.</param>
        /// <param name="smoothScroll">Enable smooth scrolling instead of discrete scrolling.</param>
        /// <returns></returns>
        public EasyOpenVrResult CreateDashboardOverlay(
            string uniqueKey,
            string title,
            out ulong mainOverlayHandle,
            out ulong thumbnailOverlayHandle,
            int textureWidth = 1920,
            int textureHeight = 1080,
            float overlayWidth = 2.67f,
            string thumbnailPath = "",
            byte[]? thumbnailBytes = null,
            bool smoothScroll = true
        )
        {
            ulong mainHandleLocal = 0;
            ulong thumbnailHandleLocal = 0;
            var error = OpenVR.Overlay.CreateDashboardOverlay(uniqueKey, title, ref mainHandleLocal, ref thumbnailHandleLocal);
            if (error == EVROverlayError.None)
            {
                // Without setting mouse scale, the overlay will act like a square, so any non-square overlay will have additional margins.
                // I presume that it is a logical surface for input detection that is included in the display calculations.
                var mouseScale = new HmdVector2_t { v0 = textureWidth, v1 = textureHeight };
                evr.DebugLog(OpenVR.Overlay.SetOverlayMouseScale(mainHandleLocal, ref mouseScale));
                evr.DebugLog(OpenVR.Overlay.SetOverlayInputMethod(mainHandleLocal, VROverlayInputMethod.Mouse));
                evr.DebugLog(OpenVR.Overlay.SetOverlayWidthInMeters(mainHandleLocal, overlayWidth));
                evr.DebugLog(OpenVR.Overlay.SetOverlayFlag(mainHandleLocal, VROverlayFlags.EnableControlBarKeyboard, true));
                evr.DebugLog(OpenVR.Overlay.SetOverlayFlag(mainHandleLocal, smoothScroll
                        ? VROverlayFlags.SendVRSmoothScrollEvents
                        : VROverlayFlags.SendVRDiscreteScrollEvents,
                    true)
                );
                evr.DebugLog(OpenVR.Overlay.SetOverlayFlag(mainHandleLocal, VROverlayFlags.ShowTouchPadScrollWheel, true));
                if (!string.IsNullOrWhiteSpace(thumbnailPath))
                {
                    OpenVR.Overlay.SetOverlayFromFile(thumbnailHandleLocal, thumbnailPath);
                }
                else if (thumbnailBytes is { Length: > 0 })
                {
                    SetOverlayTextureFromBytes(thumbnailHandleLocal, thumbnailBytes);
                }
            }

            mainOverlayHandle = mainHandleLocal;
            thumbnailOverlayHandle = thumbnailHandleLocal;
            return evr.DebugLog(error);
        }

        public EasyOpenVrResult SetOverlayTransform(ulong handle, HmdMatrix34_t transform, uint anchor = uint.MaxValue,
            ETrackingUniverseOrigin origin = ETrackingUniverseOrigin.TrackingUniverseStanding)
        {
            EVROverlayError error;
            if (anchor != uint.MaxValue)
                error = OpenVR.Overlay.SetOverlayTransformTrackedDeviceRelative(handle, anchor, ref transform);
            else error = OpenVR.Overlay.SetOverlayTransformAbsolute(handle, origin, ref transform);
            return evr.DebugLog(error);
        }

        public EasyOpenVrResult SetOverlayTextureFromFile(ulong handle, string path)
        {
            var error = OpenVR.Overlay.SetOverlayFromFile(handle, path);
            return evr.DebugLog(error);
        }

        /// <summary>
        /// Will set raw bytes as the texture of an overlay.
        /// </summary>
        /// <param name="handle">The handle of the overlay to update.</param>
        /// <param name="bytes">The bytes of a PNG image.</param>
        /// <returns></returns>
        public unsafe EasyOpenVrResult SetOverlayTextureFromBytes(ulong handle, byte[] bytes)
        {
            var error = EVROverlayError.InvalidTexture;
            fixed (byte* bytesPointer = bytes)
            {
                int width, height, channels;
                var pixels = StbImage.LoadFromMemory(bytesPointer, bytes.Length, &width, &height, &channels, 4);
                if (pixels != null)
                {
                    error = OpenVR.Overlay.SetOverlayRaw(
                        handle,
                        (IntPtr)pixels,
                        (uint)width,
                        (uint)height,
                        4); // bytes per pixel = RGBA
                    StbImage.ImageFree(pixels);
                }
                else
                {
                    var reason = Marshal.PtrToStringAnsi((IntPtr)StbImage.FailureReason());
                    evr.DebugLog($"StbImage decode FAILED: {reason}", EDebugLevel.Error);
                } 
            }

            return evr.DebugLog(error);
        }

        /// <summary>
        /// Preliminary as I have yet to figure out how to make my own textures at runtime.
        /// </summary>
        /// <param name="handle"></param>
        /// <param name="texture"></param>
        /// <returns></returns>
        public EasyOpenVrResult SetOverlayTexture(ulong handle, ref Texture_t texture)
        {
            // DXGI_FORMAT_R8G8B8A8_UNORM 
            var error = OpenVR.Overlay.SetOverlayTexture(handle, ref texture);
            return evr.DebugLog(error);
        }

        /// <summary>
        /// Sets raw overlay pixels from Bitmap, appears to crash íf going above 1mpix or near that.
        /// It's also said to be super inefficient by Valve themselves, so never use this for frequently updating overlays.
        /// </summary>
        /// <param name="handle"></param>
        /// <param name="bmp"></param>
        public void SetOverlayPixels(ulong handle, Bitmap bmp)
        {
            BitmapUtils.PointerFromBitmap(bmp, true, (pointer) =>
            {
                var bytesPerPixel = Bitmap.GetPixelFormatSize(bmp.PixelFormat) / 8;
                var error = OpenVR.Overlay.SetOverlayRaw(handle, pointer, (uint)bmp.Width, (uint)bmp.Height,
                    (uint)bytesPerPixel);
            });
        }

        public HmdMatrix34_t GetOverlayTransform(ulong handle,
            ETrackingUniverseOrigin origin = ETrackingUniverseOrigin.TrackingUniverseStanding)
        {
            var transform = new HmdMatrix34_t();
            var error = OpenVR.Overlay.GetOverlayTransformAbsolute(handle, ref origin, ref transform);
            evr.DebugLog(error);
            return transform;
        }

        /// <summary>
        /// Sets the alpha of the overlay
        /// </summary>
        /// <param name="handle"></param>
        /// <param name="alpha">Normalized 0.0-1.0</param>
        /// <returns></returns>
        public EasyOpenVrResult SetOverlayAlpha(ulong handle, float alpha)
        {
            var error = OpenVR.Overlay.SetOverlayAlpha(handle, alpha);
            return evr.DebugLog(error);
        }

        public EasyOpenVrResult SetOverlayWidth(ulong handle, float width)
        {
            var error = OpenVR.Overlay.SetOverlayWidthInMeters(handle, width);
            return evr.DebugLog(error);
        }

        public EasyOpenVrResult SetOverlayVisibility(ulong handle, bool visible)
        {
            EVROverlayError error;
            if (visible) error = OpenVR.Overlay.ShowOverlay(handle);
            else error = OpenVR.Overlay.HideOverlay(handle);
            return evr.DebugLog(error);
        }

        /// <summary>
        /// <para>Will display a direct mode keyboard, that is one that submits characters immediately.</para>
        /// <para>Technically it is running a mininmal modal keyboard with arrow keys enabled.</para>
        /// <para>Because it is minimal, there is no output buffer, so multiline is always on and input mode is always normal. The rest of the parameters are unused.</para>
        /// </summary>
        /// <param name="handle">The handle for the overlay that is receiving the input.</param>
        /// <returns></returns>
        public EasyOpenVrResult ShowDirectModeKeyboard(ulong handle)
        {
            return evr.DebugLog(OpenVR.Overlay.ShowKeyboardForOverlay(
                handle,
                (int)EGamepadTextInputMode.k_EGamepadTextInputModeNormal,
                (int)EGamepadTextInputLineMode.k_EGamepadTextInputLineModeMultipleLines,
                (int)EKeyboardFlags.KeyboardFlag_Minimal + (int)EKeyboardFlags.KeyboardFlag_Modal + (int)EKeyboardFlags.KeyboardFlag_ShowArrowKeys,
                "",
                0,
                "",
                0
            ));
        }

        public void HideKeyboard()
        {
            OpenVR.Overlay.HideKeyboard();
        }

        #region Events

        /**
         * Will have to explore this at a later date, right now my overlays are non-interactive.
         */
        public VREvent_t[] GetNewOverlayEvents(ulong overlayHandle)
        {
            var vrEvents = new List<VREvent_t>();
            var vrEvent = new VREvent_t();
            var eventSize = (uint)Marshal.SizeOf(vrEvent);
            while (OpenVR.Overlay.PollNextOverlayEvent(overlayHandle, ref vrEvent, eventSize))
            {
                vrEvents.Add(vrEvent);
            }

            return [.. vrEvents];
        }

        public delegate void VrOverlayInputHandler(in VREvent_t e);

        internal readonly Dictionary<ulong, List<VrOverlayInputHandler>> Handlers = [];

        public void RegisterForOverlayEvents(ulong handle, VrOverlayInputHandler handler)
        {
            if (!Handlers.TryGetValue(handle, out var list))
            {
                Handlers[handle] = list = [];
            }

            list.Add(handler);
        }

        public void LoadAllNewEvents()
        {
            foreach (var handle in Handlers.Keys)
            {
                LoadNewEvents(handle);
            }
        }

        public void LoadNewEvents(ulong handle)
        {
            var vrEvent = new VREvent_t();
            try
            {
                while (OpenVR.Overlay.PollNextOverlayEvent(handle, ref vrEvent, evr.Event.VrEventTSize))
                {
                    OnInputEvent(handle, ref vrEvent);
                }
            }
            catch (Exception e)
            {
                evr.DebugLog(e, "Could not get new overlay events");
            }
        }

        public void OnInputEvent(ulong handle, ref readonly VREvent_t vrEvent)
        {
            if (Handlers.TryGetValue(handle, out var list))
            {
                foreach (var handler in CollectionsMarshal.AsSpan(list))
                {
                    handler(in vrEvent);
                }
            }
            else
            {
                evr.DebugLog($"Unhandled overlay event for handle({handle}): {Enum.GetName((EVREventType)vrEvent.eventType)}");
            }
        }

        #endregion

        public ulong FindOverlay(string uniqueKey)
        {
            ulong handle = 0;
            var error = OpenVR.Overlay.FindOverlay(uniqueKey, ref handle);
            evr.DebugLog(error);
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
            evr.DebugLog(error);
            return (width == 0 || height == 0)
                ? new OverlayTextureSize()
                : new OverlayTextureSize { width = width, height = height, aspectRatio = (float)width / (float)height };
        }
    }
}