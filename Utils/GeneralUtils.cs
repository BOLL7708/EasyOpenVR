using EasyOpenVR.Data;
using EasyOpenVR.Extensions;
using Valve.VR;

namespace EasyOpenVR.Utils;

public static class GeneralUtils
{
    public static HmdMatrix34_t GetEmptyTransform()
    {
        var transform = new HmdMatrix34_t
        {
            m0 = 1,
            m5 = 1,
            m10 = 1
        };
        return transform;
    }

    public static HmdMatrix34_t GetTransformFromEuler(YPR e)
    {
        // Assuming the angles are in radians.
        // Had to switch roll and pitch here to match SteamVR
        var ch = (float)Math.Cos(e.yaw);
        var sh = (float)Math.Sin(e.yaw);
        var ca = (float)Math.Cos(e.roll);
        var sa = (float)Math.Sin(e.roll);
        var cb = (float)Math.Cos(e.pitch);
        var sb = (float)Math.Sin(e.pitch);

        return new HmdMatrix34_t
        {
            m0 = ch * ca,
            m1 = sh * sb - ch * sa * cb,
            m2 = ch * sa * sb + sh * cb,
            m4 = sa,
            m5 = ca * cb,
            m6 = -ca * sb,
            m8 = -sh * ca,
            m9 = sh * sa * cb + ch * sb,
            m10 = -sh * sa * sb + ch * cb
        };
    }
    
    public static HmdMatrix34_t MultiplyMatrixWithMatrix(HmdMatrix34_t matA, HmdMatrix34_t matB)
    {
        return new HmdMatrix34_t
        {
            // Row 0
            m0 = matA.m0 * matB.m0 + matA.m1 * matB.m4 + matA.m2 * matB.m8,
            m1 = matA.m0 * matB.m1 + matA.m1 * matB.m5 + matA.m2 * matB.m9,
            m2 = matA.m0 * matB.m2 + matA.m1 * matB.m6 + matA.m2 * matB.m10,
            m3 = matA.m0 * matB.m3 + matA.m1 * matB.m7 + matA.m2 * matB.m11 + matA.m3,

            // Row 1
            m4 = matA.m4 * matB.m0 + matA.m5 * matB.m4 + matA.m6 * matB.m8,
            m5 = matA.m4 * matB.m1 + matA.m5 * matB.m5 + matA.m6 * matB.m9,
            m6 = matA.m4 * matB.m2 + matA.m5 * matB.m6 + matA.m6 * matB.m10,
            m7 = matA.m4 * matB.m3 + matA.m5 * matB.m7 + matA.m6 * matB.m11 + matA.m7,

            // Row 2
            m8 = matA.m8 * matB.m0 + matA.m9 * matB.m4 + matA.m10 * matB.m8,
            m9 = matA.m8 * matB.m1 + matA.m9 * matB.m5 + matA.m10 * matB.m9,
            m10 = matA.m8 * matB.m2 + matA.m9 * matB.m6 + matA.m10 * matB.m10,
            m11 = matA.m8 * matB.m3 + matA.m9 * matB.m7 + matA.m10 * matB.m11 + matA.m11,
        };
    }

    public static HmdQuaternion_t QuaternionFromMatrix(HmdMatrix34_t m)
    {
        var w = Math.Sqrt(1 + m.m0 + m.m5 + m.m10) / 2.0;
        return new HmdQuaternion_t
        {
            w = w, // Scalar
            x = (m.m9 - m.m6) / (4 * w),
            y = (m.m2 - m.m8) / (4 * w),
            z = (m.m4 - m.m1) / (4 * w)
        };
    }

    public static YPR RotationMatrixToYPR(HmdMatrix34_t m)
    {
        // Had to switch roll and pitch here to match SteamVR
        var q = QuaternionFromMatrix(m);
        var test = q.x * q.y + q.z * q.w;
        switch (test)
        {
            case > 0.499:
                // singularity at north pole
                return new YPR
                {
                    yaw = 2 * Math.Atan2(q.x, q.w), // heading
                    roll = Math.PI / 2, // attitude
                    pitch = 0 // bank
                };
            case < -0.499:
                // singularity at south pole
                return new YPR
                {
                    yaw = -2 * Math.Atan2(q.x, q.w), // headingq
                    roll = -Math.PI / 2, // attitude
                    pitch = 0 // bank
                };
        }

        var sqx = q.x * q.x;
        var sqy = q.y * q.y;
        var sqz = q.z * q.z;
        return new YPR
        {
            yaw = Math.Atan2(2 * q.y * q.w - 2 * q.x * q.z, 1 - 2 * sqy - 2 * sqz), // heading
            roll = Math.Asin(2 * test), // attitude
            pitch = Math.Atan2(2 * q.x * q.w - 2 * q.y * q.z, 1 - 2 * sqx - 2 * sqz) // bank
        };
    }

    #region Measurement

    /// <summary>
    /// Returns the angle between two matrices in degrees.
    /// </summary>
    /// <param name="matOrigin"></param>
    /// <param name="matTarget"></param>
    /// <returns></returns>
    public static double AngleBetween(HmdMatrix34_t matOrigin, HmdMatrix34_t matTarget)
    {
        var vecOrigin = GetUnitVec3();
        var vecTarget = GetUnitVec3();
        vecOrigin = vecOrigin.Rotate(matOrigin);
        vecTarget = vecTarget.Rotate(matTarget);
        const double vecSize = 1.0;
        return Math.Acos(DotProduct(vecOrigin, vecTarget) / Math.Pow(vecSize, 2)) * (180 / Math.PI);
    }

    private static HmdVector3_t GetUnitVec3()
    {
        return new HmdVector3_t() { v0 = 0, v1 = 0, v2 = 1 };
    }

    private static double DotProduct(HmdVector3_t v1, HmdVector3_t v2)
    {
        return v1.v0 * v2.v0 + v1.v1 * v2.v1 + v1.v2 * v2.v2;
    }

    #endregion
}