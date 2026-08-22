using EasyOpenVR.Data.Manifest;
using Software.Boll.EasyUtils;
using Valve.VR;

namespace EasyOpenVR;

/**
 * Used to set up and instantiation an EasyOpenVr object.
 */
public class EasyOpenVrBuilder
{
    private EasyOpenVr.EasyOpenVrInitParams _initParams;

    /**
     * Build an instance but do not initialize it.
     */
    private EasyOpenVr Build()
    {
        return new EasyOpenVr(_initParams);
    }

    /**
     * Build an instance and immediately initialize it.
     */
    public EasyOpenVr BuildAndInit()
    {
        var vr = Build();
        vr.InitWorkerThread();
        return vr;
    }

    #region Setters

    /**
     * This will output debug information in the log output as well as the through the listener.
     */
    public EasyOpenVrBuilder SetDebug(bool debug)
    {
        _initParams.Debug = debug;
        return this;
    }

    /**
     * Various application types provide different features. Notably:
     * <li>Background applications will not force a runtime launch, and it will not terminate automatically if the runtime disconnects.</li>
     * <li>Overlay applications force a runtime launch, and will register for auto-launching and input reading. Requires app manifest and action manifest respectively.</li>
     */
    public EasyOpenVrBuilder SetApplicationType(EVRApplicationType appType)
    {
        _initParams.ApplicationType = appType;
        return this;
    }

    /// <summary>
    /// The VR app manifest is required to register an application for auto launch and input.
    /// </summary>
    /// <param name="path">Cannot be used with <c>..</c>, has to be a file in the current folder or a subfolder thereof.</param>
    /// <param name="vrManifestBuilder">Provide this to write the manifest to disk if it is missing.</param>
    /// <param name="overwrite">Will overwrite an existing manifest.</param>
    public EasyOpenVrBuilder SetVrAppManifest(string path, VrManifestBuilder? vrManifestBuilder = null, bool overwrite = false)
    {
        if (vrManifestBuilder != null && (overwrite || !FileUtils.FileExists(path).FileExists))
        {
            var writeTextResult = FileUtils.WriteText(path, vrManifestBuilder.BuildAndSerialize().Json);
            if (writeTextResult.Exception != null) throw writeTextResult.Exception;
        }

        _initParams.VrAppManifestPath = path.Trim();
        return this;
    }

    /// <summary>
    /// The action manifest is required for the application to listen to inputs.
    /// </summary>
    /// <param name="path">Cannot be used with <c>..</c>, has to be a file in the current folder or a subfolder thereof.</param>
    /// <param name="actionManifestBuilder">Provide this to write the manifest to disk if it is missing.</param>
    /// <param name="overwrite">Will overwrite an existing manifest.</param>
    public EasyOpenVrBuilder SetActionManifest(string path, ActionManifestBuilder? actionManifestBuilder = null, bool overwrite = false)
    {
        if (actionManifestBuilder != null && (overwrite || !FileUtils.FileExists(path).FileExists))
        {
            var writeTextResult = FileUtils.WriteText(path, actionManifestBuilder.BuildAndSerialize().Json);
            if (writeTextResult.Exception != null) throw writeTextResult.Exception;
        }

        _initParams.ActionManifestPath = path.Trim();
        return this;
    }

    /// <summary>
    /// Set the frequency at which VR events, input events and transforms are read from the runtime
    /// as well as the rate at which overlays and play space animations are run at.
    /// </summary>
    /// <remarks>
    /// <para>If this is not set the pump is disabled, and these things will have to be triggered in external code.</para>
    /// The pumpValue is used differently depending on the chosen interval.
    /// <list type="bullet">
    /// <item><description>FractionOfHmdHz : Hz = HmdHz / value</description></item>
    /// <item><description>FixedHz : Hz = value</description></item>
    /// <item><description>Millisecond : Hz = 1000 / value</description></item>
    /// </list>
    /// <b>Note</b>: Running this above headset display frequency can bog down the runtime. 
    /// </remarks>
    public EasyOpenVrBuilder SetPumpInterval(EasyOpenVr.EPumpInterval pumpInterval, double pumpValue)
    {
        _initParams.PumpInterval = pumpInterval;
        _initParams.PumpValue = pumpValue;
        return this;
    }
    
    #endregion
}