using System.Linq;
using Localizer.Backends;
using Localizer.Extensions.Configuration;
using Localizer.Plugins;
using Microsoft.Extensions.Options;

namespace Localizer.Extensions;

public class I18NextFactory : II18NextFactory
{
    private readonly ITranslationBackend _backend;
    private readonly ILanguageDetector _languageDetector;
    private readonly IOptions<I18NextOptions> _options;
    private readonly ITranslator _translator;

    public I18NextFactory(ITranslationBackend backend, ITranslator translator, ILanguageDetector languageDetector, 
        IOptions<I18NextOptions> options)
    {
        _backend = backend;
        _translator = translator;
        _languageDetector = languageDetector;
        _options = options;
    }

    public II18Next CreateInstance()
    {
        var instance = new I18NextNet(_backend, _translator, _languageDetector)
        {
            Language = _options.Value.DefaultLanguage,
            DefaultNamespace = _options.Value.DefaultNamespace,
            DetectLanguageOnEachTranslation = _options.Value.DetectLanguageOnEachTranslation
        };
        instance.SetFallbackLanguages(_options.Value.FallbackLanguages.ToArray());

        return instance;
    }
}
