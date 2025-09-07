namespace EasyOpenVR.Settings.Controls;

public static class Radio
{
    public static Factory Create(string name, int defaultValue)
    {
        return new Factory(name, EControl.Radio, EType.Int, defaultValue);
    }
}