namespace EasyOpenVR.Settings.Controls;

public static class Select
{
    public static Factory Create(string name, int defaultValue)
    {
        return new Factory(name, EControl.Select, EType.Int, defaultValue);
    }
}