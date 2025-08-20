namespace FanShop.Services;

public class ColorGenerator
{
    public string GenerateUniquePastelColor(HashSet<string> existingColors)
    {
        var random = new Random();

        string newColor;
        int attempts = 0;
        do
        {
            var hue = random.Next(0, 360);
            var saturation = random.Next(30, 60);
            var lightness = random.Next(70, 90);

            newColor = HslToHex(hue, saturation / 100.0, lightness / 100.0);
            attempts++;
        } while (existingColors.Contains(newColor) && attempts < 50);

        return newColor;
    }

    private string HslToHex(double h, double s, double l)
    {
        h /= 360.0;

        double r, g, b;

        if (s == 0)
        {
            r = g = b = l;
        }
        else
        {
            double HueToRgb(double p, double q, double t)
            {
                if (t < 0) t += 1;
                if (t > 1) t -= 1;
                if (t < 1.0 / 6) return p + (q - p) * 6 * t;
                if (t < 1.0 / 2) return q;
                if (t < 2.0 / 3) return p + (q - p) * (2.0 / 3 - t) * 6;
                return p;
            }

            var q = l < 0.5 ? l * (1 + s) : l + s - l * s;
            var p = 2 * l - q;
            r = HueToRgb(p, q, h + 1.0 / 3);
            g = HueToRgb(p, q, h);
            b = HueToRgb(p, q, h - 1.0 / 3);
        }

        var red = (int)Math.Round(r * 255);
        var green = (int)Math.Round(g * 255);
        var blue = (int)Math.Round(b * 255);

        return $"#{red:X2}{green:X2}{blue:X2}";
    }

    public bool IsValidHexColor(string color)
    {
        if (string.IsNullOrWhiteSpace(color))
            return false;

        if (!color.StartsWith("#"))
            return false;

        if (color.Length != 7)
            return false;

        return color.Skip(1).All(c => char.IsDigit(c) || (c >= 'A' && c <= 'F') || (c >= 'a' && c <= 'f'));
    }
}