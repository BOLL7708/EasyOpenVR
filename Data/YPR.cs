using Valve.VR;

namespace EasyOpenVR.Data;

public class YPR
{
    public double yaw;
    public double pitch;
    public double roll;

    public YPR()
    {
    }

    public YPR(double yaw, double pitch, double roll)
    {
        this.yaw = yaw;
        this.pitch = pitch;
        this.roll = roll;
    }

    public YPR(HmdVector3_t vec)
    {
        pitch = vec.v0;
        yaw = vec.v1;
        roll = vec.v2;
    }
}