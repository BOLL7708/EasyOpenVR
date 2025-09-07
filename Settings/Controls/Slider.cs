namespace EasyOpenVR.Settings.Controls;

public static class Slider
{
    public static Factory Create(string name, float defaultValue)
    {
        return new Factory(name, EControl.Slider, EType.Float, defaultValue);
    }
}