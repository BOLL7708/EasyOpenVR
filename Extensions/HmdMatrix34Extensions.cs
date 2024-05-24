using EasyOpenVR.Utils;
using Valve.VR;

namespace EasyOpenVR.Extensions;

public static class HmdMatrix34Extensions
{
    #region utility
    // Dunno if you like having this here, but it helped me with debugging.
    public static string ToValueString(this HmdMatrix34_t mat)
    {
        return "HmdMatrix34_t:\n"
               + $"S: {mat.m0:F3}, R: {mat.m1:F3}, R: {mat.m2:F3}, P: {mat.m3:F3},\n"
               + $"R: {mat.m4:F3}, S: {mat.m5:F3}, R: {mat.m6:F3}, P: {mat.m7:F3},\n"
               + $"R: {mat.m8:F3}, R: {mat.m9:F3}, S: {mat.m10:F3}, P: {mat.m11:F3}";
    }
    #endregion
    
    #region Translation

    public static HmdMatrix34_t Translate(this HmdMatrix34_t mat, HmdVector3_t v, bool localAxis = true)
    {
        if (!localAxis) return mat.Add(v);

        var translationMatrix = new HmdMatrix34_t
        {
            m0 = 1,
            m5 = 1,
            m10 = 1,
            m3 = v.v0,
            m7 = v.v1,
            m11 = v.v2
        };

        return mat.Multiply(translationMatrix);
    }

    public static HmdMatrix34_t Translate(this HmdMatrix34_t mat, float x, float y, float z, bool localAxis = true)
    {
        var translationVector = new HmdVector3_t
        {
            v0 = x,
            v1 = y,
            v2 = z
        };

        return mat.Translate(translationVector, localAxis);
    }

    #endregion

    #region Rotation

    private static HmdMatrix34_t RotationX(double angle, bool degrees = true)
    {
        if (degrees) angle = (Math.PI * angle / 180.0);
        return new HmdMatrix34_t
        {
            m0 = 1,
            m5 = (float)Math.Cos(angle),
            m6 = (float)-Math.Sin(angle),
            m9 = (float)Math.Sin(angle),
            m10 = (float)Math.Cos(angle),
        };
    }

    private static HmdMatrix34_t RotationY(double angle, bool degrees = true)
    {
        if (degrees) angle = (Math.PI * angle / 180.0);
        return new HmdMatrix34_t
        {
            m0 = (float)Math.Cos(angle),
            m2 = (float)Math.Sin(angle),
            m5 = 1,
            m8 = (float)-Math.Sin(angle),
            m10 = (float)Math.Cos(angle),
        };
    }

    private static HmdMatrix34_t RotationZ(double angle, bool degrees = true)
    {
        if (degrees) angle = (Math.PI * angle / 180.0);
        return new HmdMatrix34_t
        {
            m0 = (float)Math.Cos(angle),
            m1 = (float)-Math.Sin(angle),
            m4 = (float)Math.Sin(angle),
            m5 = (float)Math.Cos(angle),
            m10 = 1,
        };
    }

    public static HmdMatrix34_t RotateX(this HmdMatrix34_t mat, double angle, bool degrees = true)
    {
        return mat.Multiply(RotationX(angle, degrees));
    }

    public static HmdMatrix34_t RotateY(this HmdMatrix34_t mat, double angle, bool degrees = true)
    {
        return mat.Multiply(RotationY(angle, degrees));
    }

    public static HmdMatrix34_t RotateZ(this HmdMatrix34_t mat, double angle, bool degrees = true)
    {
        return mat.Multiply(RotationZ(angle, degrees));
    }

    public static HmdMatrix34_t Rotate(this HmdMatrix34_t mat, double angleX, double angleY, double angleZ,
        bool degrees = true)
    {
        return mat.RotateX(angleX, degrees).RotateY(angleY, degrees).RotateZ(angleZ, degrees);
    }

    public static HmdMatrix34_t Rotate(this HmdMatrix34_t mat, HmdVector3_t angles, bool degrees = true)
    {
        return mat.Rotate(angles.v0, angles.v1, angles.v2, degrees);
    }

    #endregion

    #region Multiplication

    public static HmdMatrix34_t Multiply(this HmdMatrix34_t matA, HmdMatrix34_t matB) =>
        GeneralUtils.MultiplyMatrixWithMatrix(matA, matB);

    public static HmdMatrix34_t Multiply(this HmdMatrix34_t mat, float val)
    {
        return new HmdMatrix34_t
        {
            m0 = mat.m0 * val, m1 = mat.m1 * val, m2 = mat.m2 * val, m3 = mat.m3 * val,
            m4 = mat.m4 * val, m5 = mat.m5 * val, m6 = mat.m6 * val, m7 = mat.m7 * val,
            m8 = mat.m8 * val, m9 = mat.m9 * val, m10 = mat.m10 * val, m11 = mat.m11 * val
        };
    }

    #endregion

    #region Addition

    public static HmdMatrix34_t Add(this HmdMatrix34_t matA, HmdMatrix34_t matB)
    {
        return new HmdMatrix34_t
        {
            m0 = matA.m0 + matB.m0, m1 = matA.m1 + matB.m1, m2 = matA.m2 + matB.m2, m3 = matA.m3 + matB.m3,
            m4 = matA.m4 + matB.m4, m5 = matA.m5 + matB.m5, m6 = matA.m6 + matB.m6, m7 = matA.m7 + matB.m7,
            m8 = matA.m8 + matB.m8, m9 = matA.m9 + matB.m9, m10 = matA.m10 + matB.m10, m11 = matA.m11 + matB.m11,
        };
    }

    public static HmdMatrix34_t Add(this HmdMatrix34_t mat, HmdVector3_t vec)
    {
        mat.m3 += vec.v0;
        mat.m7 += vec.v1;
        mat.m11 += vec.v2;
        return mat;
    }

    #endregion

    #region Subtraction

    public static HmdMatrix34_t Subtract(this HmdMatrix34_t matA, HmdMatrix34_t matB)
    {
        return new HmdMatrix34_t
        {
            m0 = matA.m0 - matB.m0, m1 = matA.m1 - matB.m1, m2 = matA.m2 - matB.m2, m3 = matA.m3 - matB.m3,
            m4 = matA.m4 - matB.m4, m5 = matA.m5 - matB.m5, m6 = matA.m6 - matB.m6, m7 = matA.m7 - matB.m7,
            m8 = matA.m8 - matB.m8, m9 = matA.m9 - matB.m9, m10 = matA.m10 - matB.m10, m11 = matA.m11 - matB.m11,
        };
    }

    public static HmdMatrix34_t Subtract(this HmdMatrix34_t mat, HmdVector3_t vec)
    {
        mat.m3 -= vec.v0;
        mat.m7 -= vec.v1;
        mat.m11 -= vec.v2;
        return mat;
    }

    #endregion

    #region Interpolation

    public static HmdMatrix34_t Lerp(this HmdMatrix34_t matA, HmdMatrix34_t matB, float amount)
    {
        return new HmdMatrix34_t
        {
            // Row one
            m0 = matA.m0 + (matB.m0 - matA.m0) * amount,
            m1 = matA.m1 + (matB.m1 - matA.m1) * amount,
            m2 = matA.m2 + (matB.m2 - matA.m2) * amount,
            m3 = matA.m3 + (matB.m3 - matA.m3) * amount,

            // Row two
            m4 = matA.m4 + (matB.m4 - matA.m4) * amount,
            m5 = matA.m5 + (matB.m5 - matA.m5) * amount,
            m6 = matA.m6 + (matB.m6 - matA.m6) * amount,
            m7 = matA.m7 + (matB.m7 - matA.m7) * amount,

            // Row three
            m8 = matA.m8 + (matB.m8 - matA.m8) * amount,
            m9 = matA.m9 + (matB.m9 - matA.m9) * amount,
            m10 = matA.m10 + (matB.m10 - matA.m10) * amount,
            m11 = matA.m11 + (matB.m11 - matA.m11) * amount,
        };
    }

    #endregion

    #region Transformation

    public static HmdVector3_t EulerAngles(this HmdMatrix34_t mat)
    {
        double yaw = Math.Atan2(mat.m2, mat.m10);
        double pitch = -Math.Asin(mat.m6);
        double roll = Math.Atan2(mat.m4, mat.m5);

        return new HmdVector3_t
        {
            v1 = (float)yaw,
            v0 = (float)pitch,
            v2 = (float)roll
        };
    }

    public static HmdMatrix34_t FromEuler(this HmdMatrix34_t mat, HmdVector3_t angles)
    {
        var Rx = RotationX(angles.v0, false);
        var Ry = RotationY(angles.v1, false);
        var Rz = RotationZ(angles.v2, false);
        var rotation = Ry.Multiply(Rx).Multiply(Rz);

        mat.m0 = rotation.m0;
        mat.m1 = rotation.m1;
        mat.m2 = rotation.m2;
        mat.m4 = rotation.m4;
        mat.m5 = rotation.m5;
        mat.m6 = rotation.m6;
        mat.m8 = rotation.m8;
        mat.m9 = rotation.m9;
        mat.m10 = rotation.m10;

        return mat;
    }

    #endregion

    #region Acquisition

    public static HmdVector3_t GetPosition(this HmdMatrix34_t mat)
    {
        return new HmdVector3_t
        {
            v1 = mat.m3,
            v0 = mat.m7,
            v2 = mat.m11
        };
    }

    #endregion
    
    #region Vector Operations
    public static HmdMatrix34_t AddVector(this HmdMatrix34_t m, HmdVector3_t v)
    {
        var v2 = v.Rotate(m);
        m.m3 += v2.v0;
        m.m7 += v2.v1;
        m.m11 += v2.v2;
        return m;
    }
    #endregion
}