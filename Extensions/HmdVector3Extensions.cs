using Valve.VR;

namespace EasyOpenVR.Extensions;

public static class HmdVector3Extensions
{
    public static HmdVector3_t Multiply(this HmdVector3_t vec, float val)
    {
        return new HmdVector3_t
        {
            v0 = vec.v0 * val,
            v1 = vec.v1 * val,
            v2 = vec.v2 * val
        };
    }

    public static HmdVector3_t Add(this HmdVector3_t vec, HmdVector3_t other)
    {
        return new HmdVector3_t
        {
            v0 = vec.v0 + other.v0,
            v1 = vec.v1 + other.v1,
            v2 = vec.v2 + other.v2
        };
    }

    public static double Length(this HmdVector3_t vec)
    {
        return Math.Sqrt(vec.v0 * vec.v0 + vec.v1 * vec.v1 + vec.v2 * vec.v2);
    }
    
    public static HmdVector3_t Rotate(this HmdVector3_t v, HmdMatrix34_t m)
    {
        return new HmdVector3_t
        {
            v0 = m.m0 * v.v0 + m.m1 * v.v1 + m.m2 * v.v2,
            v1 = m.m4 * v.v0 + m.m5 * v.v1 + m.m6 * v.v2,
            v2 = m.m8 * v.v0 + m.m9 * v.v1 + m.m10 * v.v2
        };
    }
    
    
    public static HmdVector3_t Invert(this HmdVector3_t vec)
    {
        vec.v0 = -vec.v0;
        vec.v1 = -vec.v1;
        vec.v2 = -vec.v2;
        return vec;
    }
}