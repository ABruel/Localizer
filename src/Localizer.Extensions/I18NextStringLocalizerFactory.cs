using System;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;

namespace Localizer.Extensions;

public class I18NextStringLocalizerFactory : IStringLocalizerFactory
{
    private readonly II18Next _i18NextNet;
    private readonly ILogger<I18NextStringLocalizer> _logger;

    public I18NextStringLocalizerFactory(II18Next i18NextNet, ILogger<I18NextStringLocalizer> logger)
    {
        _i18NextNet = i18NextNet;
        _logger = logger;
    }

    public IStringLocalizer Create(Type resourceSource)
    {
        return new I18NextStringLocalizer(_i18NextNet, _logger);
    }

    public IStringLocalizer Create(string baseName, string location)
    {
        return new I18NextStringLocalizer(_i18NextNet, _logger);
    }
}
