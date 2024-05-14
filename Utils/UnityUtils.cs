using Valve.VR;

namespace EasyOpenVR.Utils;

public static class UnityUtils
{
    public static HmdQuaternion_t MatrixToRotation(HmdMatrix34_t m)
    {
        // x and y are reversed to flip the rotation in the X axis, to convert OpenVR to Unity
        var q = new HmdQuaternion_t();
        q.w = Math.Sqrt(1.0f + m.m0 + m.m5 + m.m10) / 2.0f;
        q.x = -((m.m9 - m.m6) / (4 * q.w));
        q.y = -((m.m2 - m.m8) / (4 * q.w));
        q.z = (m.m4 - m.m1) / (4 * q.w);
        return q;
    }

    public static HmdVector3_t MatrixToPosition(HmdMatrix34_t m)
    {
        // m11 is reversed to flip the Z axis, to convert OpenVR to Unity
        var v = new HmdVector3_t();
        v.v0 = m.m3;
        v.v1 = m.m7;
        v.v2 = -m.m11;
        return v;
    }
}