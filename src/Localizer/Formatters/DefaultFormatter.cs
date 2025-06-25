using System;
using System.Globalization;
using Localizer.Plugins;

namespace Localizer.Formatters;

public class DefaultFormatter : IFormatter
{

    public DefaultFormatter()
    {
    }

    public bool CanFormat(object value, string format, string language)
    {
        return true;
    }

    public string Format(object value, string format, string language)
    {
        if (value == null)
            return null;

        if (format == null)
            return value.ToString();

        var formatString = $"{{0:{format}}}";

        try
        {
            var cultureInfo = CultureInfo.GetCultureInfo(language);
            return string.Format(cultureInfo, formatString, value);
        }
        catch (CultureNotFoundException ex)
        {
            return string.Format(CultureInfo.InvariantCulture, formatString, value);
        }
        catch
        {
            return value.ToString();
        }
    }
}
