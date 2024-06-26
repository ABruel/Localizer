﻿using System.Threading.Tasks;
using Localizer.TranslationTrees;

namespace Localizer.Backends;

public class CompositeBackend : ITranslationBackend
{
    private readonly ITranslationBackend[] _backends;

    public CompositeBackend(params ITranslationBackend[] backends)
    {
        _backends = backends;
    }

    public async Task<ITranslationTree> LoadNamespaceAsync(string language, string @namespace)
    {
        foreach (var backend in _backends)
        {
            var tree = await backend.LoadNamespaceAsync(language, @namespace);

            if (tree != null)
                return tree;
        }

        return null;
    }
}