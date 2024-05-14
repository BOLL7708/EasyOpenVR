namespace EasyOpenVR.Utils;

public class TweenUtils
{
    static public Func<float, float> GetFunc(int index)
    {
        var func = (index < interpolatorFuncs.Length && index >= 0)
            ? interpolatorFuncs[index]
            : interpolatorFuncs[0];
        return func;
    }

    private static readonly Func<float, float> Linear = value => value;
    private static readonly Func<float, float> Sine = value => (float)Math.Sin((value * Math.PI) / 2);
    private static readonly Func<float, float> Quadratic = value => RaiseToPowerTween(value, 2);
    private static readonly Func<float, float> Cubic = value => RaiseToPowerTween(value, 3);
    private static readonly Func<float, float> Quartic = value => RaiseToPowerTween(value, 4);
    private static readonly Func<float, float> Quintic = value => RaiseToPowerTween(value, 5);
    private static readonly Func<float, float> Circle = value => (float)Math.Sqrt(1 - Math.Pow(value - 1, 2));
    private static readonly Func<float, float> Back = value => BackTween(value);
    private static readonly Func<float, float> Elastic = value => ElasticTween(value);
    private static readonly Func<float, float> Bounce = value => BounceTween(value);

    private static readonly Func<float, float>[] interpolatorFuncs =
    {
        Linear,
        Sine,
        Quadratic,
        Cubic,
        Quartic,
        Quintic,
        Circle,
        Back,
        Elastic,
        Bounce
    };

    private static float RaiseToPowerTween(float value, int power)
    {
        return 1f - (float)Math.Pow(1f - value, power);
    }

    private static float BounceTween(float value)
    {
        const float n1 = 7.5625f;
        const float d1 = 2.75f;
        if (value < 1 / d1) return n1 * value * value;
        if (value < 2 / d1) return n1 * (value -= 1.5f / d1) * value + 0.75f;
        if (value < 2.5 / d1) return n1 * (value -= 2.25f / d1) * value + 0.9375f;
        return n1 * (value -= 2.625f / d1) * value + 0.984375f;
    }

    private static float ElasticTween(float value)
    {
        const float c4 = (float)(2 * Math.PI) / 3f;
        return value == 0 ? 0
            : value == 1 ? 1
            : (float)Math.Pow(2, -10 * value) * (float)Math.Sin((value * 10 - 0.75) * c4) + 1;
    }

    private static float BackTween(float value)
    {
        var c1 = 1.70158f;
        var c3 = c1 + 1;
        return 1f + c3 * (float)Math.Pow(value - 1, 3) + c1 * (float)Math.Pow(value - 1, 2);
    }
}