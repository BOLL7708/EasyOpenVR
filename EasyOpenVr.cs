using System;
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
        EPumpInterval PumpInterval,
        double PumpValue
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

    public enum EState
    {
        Idle,
        ConnectedToSteamVr,
        FailedToConnectToSteamVr,
        InitializingPump,
        RunningPump,
        ReadyToShutdown
    }

    public enum EResultType
    {
        None,
        ULong
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

        public string ResultDescription { get; set; } = "";
        public EResultType ResultType { get; set; } = EResultType.None;
        public ulong ResultULong { get; set; } = 0;
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

    public delegate void DebugMessageHandler(string message, EDebugLevel level);

    public event DebugMessageHandler? DebugMessage;

    /**
     * Used for mostly all debug handling in the library, to allow monitoring of internal events.
     */
    private void OnDebugMessage(string message, EDebugLevel level)
    {
        DebugMessage?.Invoke(message, level);
    }

    public delegate void StateHandler(EState state);

    public event StateHandler? State;

    /**
     * Will trigger when the running state is changed.
     */
    private void OnState(EState state)
    {
        State?.Invoke(state);
    }

    public delegate void PumpCycleHandler(double deltaMs);

    public event PumpCycleHandler? PumpCycle;

    /**
     * Will trigger on each pump cycle, reporting the delta time in seconds since the last cycle.
     */
    private void OnPumpCycle(double delta)
    {
        PumpCycle?.Invoke(delta);
    }

    #endregion

    #region init

    private uint _initState;

    private bool Init()
    {
        OnState(EState.Idle);
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
        if (_initState != oldState) OnState(connected ? EState.ConnectedToSteamVr : EState.FailedToConnectToSteamVr);
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
    private bool _hasAcknowledgedShutdown = false;

    internal void InitWorkerThread()
    {
        Task.Delay(1000).Wait(); // Allow the API connection to complete 
        _workerThread = new Thread(Worker);
        if (!_workerThread.IsAlive) _workerThread.Start();
    }

    private readonly CancellationTokenSource _cts = new();

    private void Worker()
    {
        // TODO
        //  Alright, the concept here. Instead of manually keeping track of indices, new devices, events, let us keep that
        //  inside the library. Make the event pump MANDATORY even if at a low Hz, then have live lists of events and
        //  transforms and indices that are continuously updated, compared to OpenVR2WS where we have a bunch of lists.

        Thread.CurrentThread.IsBackground = true;
        var token = _cts.Token;
        var hmdHz = 0;
        var firstInitComplete = false;
        var beginShutdown = false;
        var continueShutdown = false;
        var pumpEnabled = true;
        var intervalTimeSpan = TimeSpan.FromMicroseconds(1_000_000);
        var stopwatch = new Stopwatch();

        try
        {
            while (!token.IsCancellationRequested)
            {
                if (_initState > 0)
                {
                    if (!firstInitComplete)
                    {
                        #region INIT

                        OnState(EState.InitializingPump);
                        firstInitComplete = true;

                        if (_initParams.VrAppManifestPath is { Length: > 0 })
                        {
                            System.AddAppManifest(_initParams.VrAppManifestPath);
                            // TODO: Look over the auto-launch stuff in the call to System... it's a mess.
                        }

                        if (_initParams.ActionManifestPath is { Length: > 0 })
                        {
                            Input.LoadActionManifest(_initParams.ActionManifestPath);
                        }

                        Event.Register(EVREventType.VREvent_Quit, (in _) => { beginShutdown = true; }
                        );

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

                        // Without this already connected devices will not be enumerated.
                        Data.UpdateInputDeviceHandlesAndIndices();
                        Data.UpdateDeviceClassIndices();

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

                        OnState(EState.RunningPump);

                        #endregion
                    }
                    else
                    {
                        #region PUMP

                        if (!pumpEnabled)
                        {
                            if (token.WaitHandle.WaitOne(intervalTimeSpan)) break;
                        }
                        else
                        {
                            OnPumpCycle(stopwatch.Elapsed.TotalSeconds);
                            stopwatch.Restart();

                            Event.LoadAllNew(); // This loads things like the quit event that will trigger the below.
                            if (beginShutdown || token.IsCancellationRequested) break; // before any other OpenVR call

                            Overlay.LoadAllNewEvents();
                            // TODO: Update overlay animations
                            // TODO: Update chaperone animations
                            // TODO: Broadcast... poses? Is that included in inputs below maybe?

                            if (Input.HasAnyRegisteredActionSets())
                            {
                                Input.UpdateActionStates([.. Data.InputSourceHandleToInputSource.Keys], 0);
                            }

                            var sleep = intervalTimeSpan - stopwatch.Elapsed;
                            if (sleep.Ticks > 0 && token.WaitHandle.WaitOne(sleep)) break;
                        }

                        #endregion
                    }
                }
                else
                {
                    #region IDLE

                    // Idle while we attempt to init
                    if (token.WaitHandle.WaitOne(1000)) break;
                    Init();

                    #endregion
                }
            }

            if (beginShutdown && !_hasAcknowledgedShutdown)
            {
                System.AcknowledgeShutdown(); // AcknowledgeQuit_Exiting
                _hasAcknowledgedShutdown = true;
            }
        }
        finally
        {
            if (_initState > 0)
            {
                System.Shutdown(); // Limits this to the current thread
                _initState = 0;
            }

            OnState(EState.ReadyToShutdown);
        }
    }

    public void Shutdown()
    {
        _cts.Cancel();
        if (_workerThread is { IsAlive: true } && Thread.CurrentThread != _workerThread)
        {
            // Blocks the current thread preventing the final OnState call to happen before the worker has died.
            _workerThread.Join();
        }
    }

    private static TimeSpan GetIntervalTimespanFromHmdHz(int hmdHz, double fraction = 1.0)
    {
        return TimeSpan.FromMicroseconds(1_000_000.0 / hmdHz * fraction);
    }

    #endregion

    #region Debug

    public enum EDebugLevel
    {
        Verbose,
        Debug,
        Info,
        Warning,
        Error
    }

    private void DebugLog(string message, EDebugLevel level = EDebugLevel.Verbose)
    {
        if (!_initParams.Debug) return;

        var stackTrace = new StackTrace();
        var stackFrame = stackTrace.GetFrame(1);
        var methodName = stackFrame?.GetMethod()?.Name;
        var text = $"{methodName}: {message}";
        OnDebugMessage(text, level);
    }

    private EasyOpenVrResult DebugLog(Enum errorEnum, string message = "error")
    {
        var result = new EasyOpenVrResult(errorEnum, null);
        if (!_initParams.Debug || result.Success) return result;

        var stackTrace = new StackTrace();
        var stackFrame = stackTrace.GetFrame(1);
        var methodName = stackFrame?.GetMethod()?.Name;
        var text = $"{methodName} {message}: {result.ErrorType}.{result.ErrorName} ({result.ErrorOrdinal})";
        OnDebugMessage(text, EDebugLevel.Warning);
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
        OnDebugMessage(text, EDebugLevel.Warning);
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
        OnDebugMessage(text, EDebugLevel.Error);
        return result;
    }

    #endregion
}