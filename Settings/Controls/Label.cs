namespace EasyOpenVR.Settings.Controls;

/**
 * A label control, will display a label. Explore functionality.
 */
public static class Label
{
    public static Factory Create(string name)
    {
        return new Factory(name, EControl.Label, EType.None, null);
    }
}