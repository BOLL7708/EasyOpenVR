namespace EasyOpenVR.Settings.Controls;

public static class Checkbox
{
    public static Factory Create(string name, bool defaultValue)
    {
        return new Factory(name, EControl.Checkbox, EType.Bool, defaultValue);
    }
}