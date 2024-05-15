namespace EasyOpenVR.Utils;

/**
 * Based on Easings from https://easings.net/
 * Initial conversion made by https://chatgpt.com
 */
public static class EasingUtils
{
    public enum EasingMode
    {
        In,
        Out,
        InOut
    }
    public enum EasingType
    {
        Linear,
        Sine,
        Quad,
        Cubic,
        Quart,
        Quint,
        Expo,
        Circ,
        Back,
        Elastic,
        Bounce
    }
    public static Func<double, double> Get(EasingType type, EasingMode mode)
    {
        var collection = mode switch
        {
            EasingMode.In => InFuncs,
            EasingMode.Out => OutFuncs,
            EasingMode.InOut => InOutFuncs,
            _ => InFuncs
        };
        var index = (int)type;
        var func = (index < collection.Length && index >= 0)
            ? collection[index]
            : collection[0];
        return func;
    }
    
    private static readonly Func<double, double>[] InFuncs =
    [
        Linear,
        EaseInSine,
        EaseInQuad,
        EaseInCubic,
        EaseInQuart,
        EaseInQuint,
        EaseInExpo,
        EaseInCirc,
        EaseInBack,
        EaseInElastic,
        EaseInBounce
    ];
    private static readonly Func<double, double>[] OutFuncs =
    [
        Linear,
        EaseOutSine,
        EaseOutQuad,
        EaseOutCubic,
        EaseOutQuart,
        EaseOutQuint,
        EaseOutExpo,
        EaseOutCirc,
        EaseOutBack,
        EaseOutElastic,
        EaseOutBounce
    ];
    private static readonly Func<double, double>[] InOutFuncs =
    [
        Linear,
        EaseInOutSine,
        EaseInOutQuad,
        EaseInOutCubic,
        EaseInOutQuart,
        EaseInOutQuint,
        EaseInOutExpo,
        EaseInOutCirc,
        EaseInOutBack,
        EaseInOutElastic,
        EaseInOutBounce
    ];

    private static double Linear(double t)
    {
        return t;
    }

    private static double EaseInSine(double t)
    {
        return 1 - Math.Cos((t * Math.PI) / 2);
    }

    private static double EaseOutSine(double t)
    {
        return Math.Sin((t * Math.PI) / 2);
    }

    private static double EaseInOutSine(double t)
    {
        return -(Math.Cos(Math.PI * t) - 1) / 2;
    }

    private static double EaseInQuad(double t)
    {
        return t * t;
    }

    private static double EaseOutQuad(double t)
    {
        return 1 - (1 - t) * (1 - t);
    }

    private static double EaseInOutQuad(double t)
    {
        return t < 0.5 
            ? 2 * t * t 
            : 1 - Math.Pow(-2 * t + 2, 2) / 2;
    }

    private static double EaseInCubic(double t)
    {
        return t * t * t;
    }

    private static double EaseOutCubic(double t)
    {
        return 1 - Math.Pow(1 - t, 3);
    }

    private static double EaseInOutCubic(double t)
    {
        return t < 0.5 
            ? 4 * t * t * t 
            : 1 - Math.Pow(-2 * t + 2, 3) / 2;
    }

    private static double EaseInQuart(double t)
    {
        return t * t * t * t;
    }

    private static double EaseOutQuart(double t)
    {
        return 1 - Math.Pow(1 - t, 4);
    }

    private static double EaseInOutQuart(double t)
    {
        return t < 0.5 
            ? 8 * t * t * t * t : 
            1 - Math.Pow(-2 * t + 2, 4) / 2;
    }

    private static double EaseInQuint(double t)
    {
        return t * t * t * t * t;
    }

    private static double EaseOutQuint(double t)
    {
        return 1 - Math.Pow(1 - t, 5);
    }

    private static double EaseInOutQuint(double t)
    {
        return t < 0.5 
            ? 16 * t * t * t * t * t 
            : 1 - Math.Pow(-2 * t + 2, 5) / 2;
    }

    private static double EaseInExpo(double t)
    {
        return t == 0 
            ? 0 
            : Math.Pow(2, 10 * t - 10);
    }

    private static double EaseOutExpo(double t)
    {
        return t == 1 
            ? 1 
            : 1 - Math.Pow(2, -10 * t);
    }

    private static double EaseInOutExpo(double t)
    {
        return t == 0 
            ? 0 
            : t == 1
                ? 1 
                : t < 0.5 
                    ? Math.Pow(2, 20 * t - 10) / 2 
                    : (2 - Math.Pow(2, -20 * t + 10)) / 2;
    }

    private static double EaseInCirc(double t)
    {
        return 1 - Math.Sqrt(1 - Math.Pow(t, 2));
    }

    private static double EaseOutCirc(double t)
    {
        return Math.Sqrt(1 - Math.Pow(t - 1, 2));
    }

    private static double EaseInOutCirc(double t)
    {
        return t < 0.5 
            ? (1 - Math.Sqrt(1 - Math.Pow(2 * t, 2))) / 2 
            : (Math.Sqrt(1 - Math.Pow(-2 * t + 2, 2)) + 1) / 2;
    }

    private static double EaseInBack(double t)
    {
        const double c1 = 1.70158;
        const double c3 = c1 + 1;
        return c3 * t * t * t - c1 * t * t;
    }

    private static double EaseOutBack(double t)
    {
        const double c1 = 1.70158;
        const double c3 = c1 + 1;
        return 1 + c3 * Math.Pow(t - 1, 3) + c1 * Math.Pow(t - 1, 2);
    }

    private static double EaseInOutBack(double t)
    {
        const double c1 = 1.70158;
        const double c2 = c1 * 1.525;
        return t < 0.5 
            ? (Math.Pow(2 * t, 2) * ((c2 + 1) * 2 * t - c2)) / 2 
            : (Math.Pow(2 * t - 2, 2) * ((c2 + 1) * (t * 2 - 2) + c2) + 2) / 2;
    }

    private static double EaseInElastic(double t)
    {
        const double c4 = (2 * Math.PI) / 3;
        return t == 0 
            ? 0 
            : t == 1 
                ? 1 
                : -Math.Pow(2, 10 * t - 10) * Math.Sin((t * 10 - 10.75) * c4);
    }

    private static double EaseOutElastic(double t)
    {
        const double c4 = (2 * Math.PI) / 3;
        return t == 0 
            ? 0 
            : t == 1 
                ? 1 
                : Math.Pow(2, -10 * t) * Math.Sin((t * 10 - 0.75) * c4) + 1;
    }

    private static double EaseInOutElastic(double t)
    {
        const double c5 = (2 * Math.PI) / 4.5;
        return t == 0 
            ? 0 
            : t == 1 
                ? 1 
                : t < 0.5 
                    ? -(Math.Pow(2, 20 * t - 10) * Math.Sin((20 * t - 11.125) * c5)) / 2 
                    : (Math.Pow(2, -20 * t + 10) * Math.Sin((20 * t - 11.125) * c5)) / 2 + 1;
    }

    private static double EaseInBounce(double t)
    {
        return 1 - EaseOutBounce(1 - t);
    }

    private static double EaseOutBounce(double t)
    {
        const double n1 = 7.5625;
        const double d1 = 2.75;
        if (t < 1 / d1) return n1 * t * t;
        if (t < 2 / d1) return n1 * (t -= 1.5 / d1) * t + 0.75;
        if (t < 2.5 / d1) return n1 * (t -= 2.25 / d1) * t + 0.9375;
        return n1 * (t -= 2.625 / d1) * t + 0.984375;
    }

    private static double EaseInOutBounce(double t)
    {
        return t < 0.5 
            ? (1 - EaseOutBounce(1 - 2 * t)) / 2 
            : (1 + EaseOutBounce(2 * t - 1)) / 2;
    }
}