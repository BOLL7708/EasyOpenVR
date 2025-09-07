namespace EasyOpenVR.Settings.Controls;

public static class Toggle
{
    public static Factory Create(string name, bool defaultValue)
    {
        return new Factory(name, EControl.Toggle, EType.Bool, defaultValue);
    }
}