﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Localizer.Backends;
using Localizer.Internal;
using Localizer.TranslationTrees;

namespace Localizer.Plugins;

public class DefaultTranslator : ITranslator
{
    private readonly ITranslationBackend _backend;
    private readonly IInterpolator _interpolator;
    private readonly IPluralResolver _pluralResolver;

    private readonly ConcurrentDictionary<string, ITranslationTree> _treeCache = new();

    public DefaultTranslator(ITranslationBackend backend, IPluralResolver pluralResolver, IInterpolator interpolator)
    {
        _backend = backend;
        _pluralResolver = pluralResolver;
        _interpolator = interpolator;
    }

    public DefaultTranslator(ITranslationBackend backend)
    {
        _backend = backend;
        _pluralResolver = new DefaultPluralResolver();
        _interpolator = new DefaultInterpolator();
    }

    public DefaultTranslator(ITranslationBackend backend, IInterpolator interpolator)
    {
        _backend = backend;
        _pluralResolver = new DefaultPluralResolver();
        _interpolator = interpolator;
    }

    public bool AllowInterpolation { get; set; } = true;

    public bool AllowNesting { get; set; } = true;

    public bool AllowPostprocessing { get; set; } = true;

    public string ContextSeparator { get; set; } = "_";

    public List<IMissingKeyHandler> MissingKeyHandlers { get; } = new();

    public event EventHandler<MissingKeyEventArgs> MissingKey;

    public List<IPostProcessor> PostProcessors { get; } = new();

    public virtual async Task<string> TranslateAsync(string language, string key, IDictionary<string, object> args, TranslationOptions options)
    {
        if (string.IsNullOrWhiteSpace(language))
            throw new ArgumentNullException(nameof(language));
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentNullException(nameof(key));
        if (options == null)
            throw new ArgumentNullException(nameof(options));

        args.TryGetValue("Namespace", out var actualNamespace);
        actualNamespace = actualNamespace as string ?? options.DefaultNamespace;

        if (language.ToLower() == "cimode")
            return $"{actualNamespace}:{key}";
        var result = await ResolveTranslationAsync(language, actualNamespace.ToString(), key, args, options);

        if (result == null)
            throw new TranslationNotFoundException(await ExtendTranslationAsync(key, key, language, args, options));

        var a = await ExtendTranslationAsync(result, key, language, args, options);

        return a;
    }

    private static bool CheckForSpecialArg(IDictionary<string, object> args, string key, params Type[] allowedTypes)
    {
        if (args == null)
            return false;

        if (!args.ContainsKey(key))
            return false;

        var value = args[key];

        for (var i = 0; i < allowedTypes.Length; i++)
        {
            var type = allowedTypes[i];

            if (value.GetType() == type)
                return true;
        }

        return false;
    }

    private async Task<string> ExtendTranslationAsync(string result, string key, string language, IDictionary<string, object> args,
        TranslationOptions options)
    {
        IDictionary<string, object> replaceArgs;

        if ((args?.ContainsKey("replace") ?? false) && args["replace"].GetType().IsClass)
            replaceArgs = args["replace"].ToDictionary();
        else
            replaceArgs = args;

        if (AllowInterpolation && (!(args?.ContainsKey("interpolate") ?? false) || args["interpolate"] is bool interpolate && interpolate))
            result = await _interpolator.InterpolateAsync(result, key, language, replaceArgs);

        if (AllowNesting && (!(args?.ContainsKey("nest") ?? false) || args["nest"] is bool nest && nest) && _interpolator.CanNest(result))
            result = await _interpolator.NestAsync(result, language, replaceArgs,
                (lang2, key2, args2) => TranslateAsync(lang2, key2, args2, options));

        if (AllowPostprocessing && PostProcessors.Count > 0)
            result = HandlePostProcessing(result, language, key, args);

        return result;
    }

    private string[] GetPostProcessorKeys(IDictionary<string, object> args)
    {
        if (args == null)
            return null;

        if (!args.ContainsKey("postProcess"))
            return null;

        if (args["postProcess"] is string postProcessorStr)
        {
            if (postProcessorStr.IndexOf(',') > -1)
                return postProcessorStr.Split(",", StringSplitOptions.RemoveEmptyEntries).Select(x => x.Trim()).ToArray();

            return new[] { postProcessorStr };
        }

        var localArgs = args["postProcess"];
        var postProcessType = localArgs.GetType();
        if (postProcessType.IsArray && postProcessType.HasElementType && postProcessType.GetElementType() == typeof(string))
            return localArgs as string[];

        return null;
    }

    private string HandlePostProcessing(string result, string language, string key, IDictionary<string, object> args)
    {
        var postProcessorKeys = GetPostProcessorKeys(args);

        if (postProcessorKeys != null)
            foreach (var postProcessorKey in postProcessorKeys)
            {
                if (string.IsNullOrWhiteSpace(postProcessorKey))
                    continue;

                foreach (var postProcessor in PostProcessors)
                {
                    if (postProcessor.Keyword == postProcessorKey)
                        result = postProcessor.ProcessResult(key, result, args, language, this);
                }
            }

        return result;
    }

    private async Task OnMissingKey(string language, string @namespace, string key, List<string> possibleKeys)
    {
        if (MissingKey == null && MissingKeyHandlers.Count == 0)
            return;

        var args = new MissingKeyEventArgs(language, @namespace, key, possibleKeys.ToArray());

        MissingKey?.Invoke(this, args);

        if (MissingKeyHandlers.Count > 0)
        {
            foreach (var missingKeyHandler in MissingKeyHandlers)
                await missingKeyHandler.HandleMissingKeyAsync(this, args);
        }
    }

    private async Task<string> ResolveTranslationNoFallbackAsync(string language, string ns, string key, IDictionary<string, object> args)
    {
        var translationTree = await ResolveTranslationTreeAsync(language, ns);

        if (translationTree == null)
        {
            return null;
        }

        var needsPluralHandling = CheckForSpecialArg(args, "count", typeof(int), typeof(long)) && _pluralResolver.NeedsPlural(language);
        var needsContextHandling = CheckForSpecialArg(args, "context", typeof(string));

        var finalKey = key;
        var possibleKeys = new List<string>
        {
            finalKey
        };
        var pluralSuffix = string.Empty;

        if (needsPluralHandling)
        {
            var count = (int)Convert.ChangeType(args["count"], typeof(int));
            pluralSuffix = _pluralResolver.GetPluralSuffix(language, count);

            // Fallback for plural if context was not found
            if (needsContextHandling)
                possibleKeys.Add($"{finalKey}{pluralSuffix}");
        }

        // Get key for context if needed
        if (needsContextHandling)
        {
            var context = (string)args["context"];
            finalKey = $"{finalKey}{ContextSeparator}{context}";
            possibleKeys.Add(finalKey);
        }

        // Get key for plural if needed
        if (needsPluralHandling)
        {
            finalKey = $"{finalKey}{pluralSuffix}";
            possibleKeys.Add(finalKey);
        }

        string result = null;

        // Iterate over the possible keys starting with most specific pluralkey (-> contextkey only) -> singularkey only
        for (var i = possibleKeys.Count - 1; i >= 0; i--)
        {
            var currentKey = possibleKeys[i];
            result = translationTree.GetValue(currentKey, args);

            if (result != null)
                break;

        }

        if (result == null)
            await OnMissingKey(language, ns, key, possibleKeys);

        return result;
    }

    private async Task<string> ResolveTranslationAsync(string language, string ns, string key, IDictionary<string, object> args, TranslationOptions options)
    {
        var result = await ResolveTranslationNoFallbackAsync(language, ns, key, args);

        if (result == null && options?.FallbackNamespaces?.Length > 0)
        {
            foreach (var fallbackNamespace in options.FallbackNamespaces)
            {
                var fallbackResult = await ResolveTranslationNoFallbackAsync(language, fallbackNamespace, key, args);
                if (fallbackResult != null)
                    return fallbackResult;
            }
        }

        if (result == null && options?.FallbackLanguages?.Length > 0)
        {
            foreach (var fallbackLanguage in options.FallbackLanguages)
            {
                var fallbackResult = await ResolveTranslationNoFallbackAsync(fallbackLanguage, ns, key, args);
                if (fallbackResult != null)
                    return fallbackResult;

                if (options.FallbackNamespaces?.Length > 0)
                {
                    foreach (var fallbackNamespace in options.FallbackNamespaces)
                    {
                        fallbackResult = await ResolveTranslationNoFallbackAsync(fallbackLanguage, fallbackNamespace, key, args);
                        if (fallbackResult != null)
                            return fallbackResult;
                    }
                }
            }
        }

        return result;
    }

    private async Task<ITranslationTree> ResolveTranslationTreeAsync(string language, string ns)
    {
        var cacheKey = $"{language}.{ns}";

        if (_treeCache.TryGetValue(cacheKey, out var tree))
            return tree;

        tree = await _backend.LoadNamespaceAsync(language, ns);

        _treeCache.TryAdd(cacheKey, tree);

        return tree;
    }
}
