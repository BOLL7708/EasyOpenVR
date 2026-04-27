using System;
using System.Buffers;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Valve.VR;

namespace EasyOpenVR;

public partial class EasyOpenVr
{
    public ChaperoneMethods Chaperone { get; }
    public DeviceMethods Device { get; }
    public EventMethods Event { get; }
    public InputMethods Input { get; }
    public NotificationMethods Notification { get; }
    public OverlayMethods Overlay { get; }
    public ScreenshotMethods Screenshot { get; }
    public SettingMethods Setting { get; }
    public StatisticsMethods Statistics { get; }
    public SystemMethods System { get; }
    public VideoMethods Video { get; }
    public DataStore Data { get; }

    public record struct EasyOpenVrInitParams(
        EVRApplicationType ApplicationType,
        string? VrAppManifestPath,
        string? ActionManifestPath,
        bool Debug,
        bool RegisterAutoLaunch,
        bool ForceAutoLaunch,
        EPumpInterval PumpInterval,
        double PumpValue,
        bool QuitWithRuntime
        // TODO: Add stuff for input actions? Are they one-time only set-at-start stuff? Figure this out.
    )
    {
    }

    public enum EPumpInterval
    {
        None,
        FractionOfHmdHz,
        FixedHz,
        Millisecond
    }

    public record struct EasyOpenVrResult(
        Enum? Error,
        Enum? Value,
        string Message = ""
    )
    {
        public int ErrorOrdinal => Error == null ? -1 : (int)Convert.ChangeType(Error, Error.GetTypeCode());
        public string ErrorType => Error == null ? "" : Error.GetType().Name;
        public string ErrorName => Error == null ? "" : Enum.GetName(Error.GetType(), Error) ?? "";
        public int ValueOrdinal => Value == null ? -1 : (int)Convert.ChangeType(Value, Value.GetTypeCode());
        public string ValueType => Value == null ? "" : Value.GetType().Name;
        public string ValueName => Value == null ? "" : Enum.GetName(Value.GetType(), Value) ?? "";
        public bool Success => ErrorOrdinal == 0;
    }


    public EasyOpenVr(EasyOpenVrInitParams initParams)
    {
        _initParams = initParams;
        Chaperone = new ChaperoneMethods(this);
        Device = new DeviceMethods(this);
        Event = new EventMethods(this);
        Input = new InputMethods(this);
        Notification = new NotificationMethods(this);
        Overlay = new OverlayMethods(this);
        Screenshot = new ScreenshotMethods(this);
        Setting = new SettingMethods(this);
        Statistics = new StatisticsMethods(this);
        System = new SystemMethods(this);
        Video = new VideoMethods(this);
        Data = new DataStore(this);
    }

    private readonly EasyOpenVrInitParams _initParams;
    private readonly Random _random = new();

    #region Events

    public delegate void DebugMessageHandler(string message);

    public event DebugMessageHandler? DebugMessage;

    /**
     * Used for mostly all debug handling in the library, to allow monitoring of internal events.
     */
    private void OnDebugMessage(string message)
    {
        DebugMessage?.Invoke(message);
    }

    public delegate void StateHandler(bool connected);

    public event StateHandler? State;

    /**
     * Will trigger when the running state is changed.
     */
    private void OnState(bool connected)
    {
        State?.Invoke(connected);
    }

    #endregion

    #region init

    private uint _initState;

    private bool Init()
    {
        var error = EVRInitError.Unknown;
        var oldState = _initState;
        try
        {
            _initState = OpenVR.InitInternal(ref error, _initParams.ApplicationType);
        }
        catch (Exception e)
        {
            DebugLog(e, "You might be building for 32bit with a 64bit .dll, error");
        }

        var connected = error == EVRInitError.None && _initState > 0;
        if (_initState != oldState) OnState(connected);
        DebugLog(error);
        return connected;
    }

    public bool IsInitialized()
    {
        return _initState > 0;
    }

    #endregion

    #region Worker

    private Thread? _workerThread;

    internal void InitWorkerThread()
    {
        Task.Delay(1000).Wait(); // Allow the API connection to complete 
        _workerThread = new Thread(Worker);
        if (!_workerThread.IsAlive) _workerThread.Start();
    }

    private void Worker()
    {
        /* TODO
         Alright, the concept here. Instead of manually keeping track of indices, new devices, events, let us keep that
         inside the library. Make the event pump MANDATORY even if at a low Hz, then have live lists of events and
         transforms and indices that are continuously updated, compared to OpenVR2WS where we have a bunch of lists.
         */

        Thread.CurrentThread.IsBackground = true;
        var hmdHz = 0;
        var firstInitComplete = false;
        var shouldQuit = false;
        var pumpEnabled = true;
        var intervalTimeSpan = TimeSpan.FromMicroseconds(1_000_000);
        var stopwatch = new Stopwatch();

        while (true)
        {
            if (_initState > 0)
            {
                #region INIT

                if (!firstInitComplete)
                {
                    firstInitComplete = true;

                    if (_initParams.VrAppManifestPath is { Length: > 0 })
                    {
                        System.LoadAppManifest(_initParams.VrAppManifestPath);
                        // TODO: Look over the auto-launch stuff in the call to System... it's a mess.
                    }

                    if (_initParams.ActionManifestPath is { Length: > 0 })
                    {
                        Input.LoadActionManifest(_initParams.ActionManifestPath);
                    }

                    if (_initParams.QuitWithRuntime)
                    {
                        Event.Register(EVREventType.VREvent_Quit,
                            (in _) =>
                            {
                                // TODO: I think we should always disconnect if the runtime quits
                                //  OpenVR2WS also indicates it should stop running...
                                
                                // TODO: MAKE QUITTING ON STEAM QUIT MANDATORY? PROBABLY?
                                //  MAYBE ALSO MAKE LAUNCH WITH STEAM DEFAULT?
                                //  IT SEEMS LIKE A BAD THING TO FORCE STEAMVR TO RELAUNCH REPEATEDLY WHEN NOT BEING A SCENE APP
                                shouldQuit = true;
                            }
                        );
                    }

                    switch (_initParams.PumpInterval)
                    {
                        // When using this pump mode we need to keep track of the headset display frequency.
                        case EPumpInterval.FractionOfHmdHz:
                        {
                            // Initial retrieval of value
                            Data.UpdateDeviceClassIndices();
                            var hmdIndex = Data.DeviceClassToTrackedDeviceIndices[ETrackedDeviceClass.HMD].First();
                            hmdHz = (int)Math.Round(Device.GetFloatTrackedDeviceProperty(
                                hmdIndex,
                                ETrackedDeviceProperty.Prop_DisplayFrequency_Float
                            ));
                            intervalTimeSpan = GetIntervalTimespanFromHmdHz(hmdHz, _initParams.PumpValue);

                            // Registration of listener for change of value
                            Event.Register(EVREventType.VREvent_PropertyChanged, (in vrEvent) =>
                            {
                                if (vrEvent.data.property.prop != ETrackedDeviceProperty.Prop_DisplayFrequency_Float) return;
                                hmdHz = (int)Math.Round(Device.GetFloatTrackedDeviceProperty(
                                    vrEvent.trackedDeviceIndex,
                                    ETrackedDeviceProperty.Prop_DisplayFrequency_Float
                                ));
                                intervalTimeSpan = GetIntervalTimespanFromHmdHz(hmdHz, _initParams.PumpValue);
                            });
                            break;
                        }
                        case EPumpInterval.FixedHz:
                        {
                            intervalTimeSpan = TimeSpan.FromMicroseconds(1_000_000.0 / _initParams.PumpValue);
                            break;
                        }
                        case EPumpInterval.Millisecond:
                        {
                            intervalTimeSpan = TimeSpan.FromMilliseconds(_initParams.PumpValue);
                            break;
                        }
                        case EPumpInterval.None:
                        default:
                        {
                            pumpEnabled = false;
                            break;
                        }
                    }

                    DebugLog(pumpEnabled ? $"Pump interval is: {intervalTimeSpan.TotalMilliseconds}ms" : "Pump is disabled.");

                    Event.Register(EVREventType.VREvent_TrackedDeviceActivated, (in vrEvent) =>
                        {
                            Data.UpdateInputDeviceHandlesAndIndices();
                            Data.UpdateDeviceClassIndices(vrEvent.trackedDeviceIndex);
                        }
                    );

                    Event.Register([
                            EVREventType.VREvent_TrackedDeviceDeactivated,
                            EVREventType.VREvent_TrackedDeviceRoleChanged,
                            EVREventType.VREvent_TrackedDeviceUpdated
                        ], (in _) =>
                        {
                            Data.UpdateInputDeviceHandlesAndIndices();
                            Data.UpdateDeviceClassIndices();
                        }
                    );

                    Event.Register(EVREventType.VREvent_None,
                        (in ev) =>
                        {
                            DebugLog("!!! [NONE] EVENT DETECTED!"); // TODO
                        }
                    );

                    #endregion
                }
                else
                {
                    #region PUMP

                    if (!pumpEnabled)
                    {
                        Thread.Sleep(intervalTimeSpan);
                        continue; // Disabled
                    }

                    stopwatch.Restart();

                    // LOAD ALL EVENTS - EMIT EVENTS
                    Event.LoadAllNew();
                    // - ACT ON CERTAIN EVENTS TO RELOAD LISTS, ROLES, EXIT, ETC, THINGS USED IN OTHER FEATURES BELOW
                    // LOAD POSES - EMIT EVENTS
                    // LOAD INPUTS - EMIT EVENTS
                    // LOAD STATISTICS - EMIT EVENTS
                    // UPDATE OVERLAY ANIMATIONS
                    // UPDATE CHAPERONE ANIMATIONS

                    // Sleep for the rest of the cycle so we don't update too fast, that will impact SteamVR.
                    var sleepTimeSpan = intervalTimeSpan - stopwatch.Elapsed;
                    if (sleepTimeSpan.Ticks > 0) Thread.Sleep(sleepTimeSpan);

                    #endregion
                }
            }
            else
            {
                #region IDLE

                // Idle with attempted init
                Thread.Sleep(1000);
                Init(); // TODO: This seems to restart SteamVR which I'm not sure it should... figure out if we can control it. 

                #endregion
            }

            if (!shouldQuit) continue;
            shouldQuit = false;
            firstInitComplete = false;
            System.AcknowledgeShutdown();
            System.Shutdown();
            OnState(false);
            if (_initParams.QuitWithRuntime)
            {
                DebugLog("Quitting with runtime, shutting down pump.");
                return;
            }
            
            // TODO: Reset local collections? Rest should be reset in System.Shutdown() above, maybe look that over.
            
            DebugLog("Shutting down EasyOpenVR due to the connected runtime quitting.");
        }
    }

    private static TimeSpan GetIntervalTimespanFromHmdHz(int hmdHz, double fraction = 1.0)
    {
        return TimeSpan.FromMicroseconds(1_000_000.0 / hmdHz * fraction);
    }

    #endregion

    #region Debug

    private void DebugLog(string message)
    {
        if (!_initParams.Debug) return;

        var stackTrace = new StackTrace();
        var stackFrame = stackTrace.GetFrame(1);
        var methodName = stackFrame?.GetMethod()?.Name;
        var text = $"{methodName}: {message}";
        OnDebugMessage(text);
    }

    private EasyOpenVrResult DebugLog(Enum errorEnum, string message = "error")
    {
        var result = new EasyOpenVrResult(errorEnum, null);
        if (!_initParams.Debug || result.Success) return result;

        var stackTrace = new StackTrace();
        var stackFrame = stackTrace.GetFrame(1);
        var methodName = stackFrame?.GetMethod()?.Name;
        var text = $"{methodName} {message}: {result.ErrorType}.{result.ErrorName} ({result.ErrorOrdinal})";
        OnDebugMessage(text);
        result.Message = text;
        return result;
    }

    private EasyOpenVrResult DebugLog(Enum errorEnum, Enum valueEnum)
    {
        var result = new EasyOpenVrResult(errorEnum, valueEnum);
        if (!_initParams.Debug || result.Success) return result;

        var stackTrace = new StackTrace();
        var stackFrame = stackTrace.GetFrame(1);
        var methodName = stackFrame?.GetMethod()?.Name;
        var text =
            $"{methodName} {result.ValueType}.{result.ValueName}: {result.ErrorType}.{result.ErrorName}";
        OnDebugMessage(text);
        result.Message = text;
        return result;
    }

    private EasyOpenVrResult DebugLog(Exception e, string message = "error")
    {
        if (!_initParams.Debug) return new EasyOpenVrResult(null, null);

        var stackTrace = new StackTrace();
        var stackFrame = stackTrace.GetFrame(1);
        var methodName = stackFrame?.GetMethod()?.Name;
        var text = $"{methodName} {message}: {e.Message}";
        var result = new EasyOpenVrResult(null, null, text);
        OnDebugMessage(text);
        return result;
    }

    #endregion
}