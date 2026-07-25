using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Valve.VR;

namespace EasyOpenVR;

public partial class EasyOpenVr
{
    public class EventMethods(EasyOpenVr evr)
    {
        internal readonly uint VrEventTSize = (uint)Marshal.SizeOf<VREvent_t>();

        public delegate void VrEventHandler(in VREvent_t e);

        internal readonly Dictionary<EVREventType, List<VrEventHandler>> Handlers = [];

        public void Register(EVREventType type, VrEventHandler handler)
        {
            Register([type], handler);
        }

        public void Register(EVREventType[] types, VrEventHandler handler)
        {
            foreach (var type in types)
            {
                if (!Handlers.TryGetValue(type, out var list))
                {
                    Handlers[type] = list = [];
                }

                list.Add(handler);
            }
        }

        public void Unregister(EVREventType type, VrEventHandler handler)
        {
            Unregister([type], handler);
        }

        public void Unregister(EVREventType[] types, VrEventHandler handler)
        {
            foreach (var type in types)
            {
                if (Handlers.TryGetValue(type, out var list))
                {
                    list.Remove(handler);
                }
            }
        }

        private void OnEvent(ref readonly VREvent_t vrEvent)
        {
            var type = (EVREventType)vrEvent.eventType;
            if (Handlers.TryGetValue(type, out var list))
            {
                foreach (var handler in CollectionsMarshal.AsSpan(list))
                {
                    handler(in vrEvent);
                }
            }
            else
            {
                evr.DebugLog($"Unhandled event: {Enum.GetName((EVREventType) vrEvent.eventType)}");
                // TODO: Output unhandled events somehow?
            }
        }

        ///<summary>Will get all new events in the queue, note that this will cancel out triggering any registered events when running the pump, as it is also using this.</summary>
        public void LoadAllNew()
        {
            try
            {
                var vrEvent = new VREvent_t();
                while (OpenVR.System.PollNextEvent(ref vrEvent, VrEventTSize))
                {
                    OnEvent(ref vrEvent);
                }
            }
            catch (Exception e)
            {
                evr.DebugLog(e, "Could not get new events");
            }
        }
    }
}