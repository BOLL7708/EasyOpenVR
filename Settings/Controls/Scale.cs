namespace EasyOpenVR.Settings.Controls;

public static class Scale
{
    public static Factory Create(string name, EType type, object defaultValue) // TODO: Unclear what Scale is or takes as type.
    {
        return new Factory(name, EControl.Scale, type, defaultValue);
    }
}