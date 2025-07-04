﻿using System.Collections.Generic;
using System.Linq;
using System.Xml;

namespace Localizer.TranslationTrees;

public class TranslationTree : ITranslationTree
{
    public TranslationTree(TranslationGroup rootNode)
    {
        Root = rootNode;
    }

    public TranslationTreeNode Root { get; set; }

    public IDictionary<string, string> GetAllValues()
    {
        var result = new Dictionary<string, string>();

        if (Root == null)
            return result;

        MapTranslationGroup(result, (TranslationGroup)Root);

        return result;
    }

    public string GetValue(string key, IDictionary<string, object> args)
    {
        var parts = new string[] { key };

        var node = Root;

        for (var i = 0; i < parts.Length; i++)
        {
            var part = parts[i];

            if (node is TranslationGroup group)
            {

                var foundNode = group.Children[part];

                if (foundNode != null)
                    node = foundNode;
                else
                    return null;

                continue;
            }

            if (i < parts.Length)
                throw new TranslationKeyInvalidException(key,
                    $"The key `{key}` ends up in a final translation at part `{part}`. Cannot go down further the translation tree. Please check the key you've provided.");
        }

        if (node is TranslationGroup)
            throw new TranslationKeyInvalidException(key,
                $"The key `{key}` leads to a group of translations. Unable to resolve a final value for the given key. Please check the key you've provided.");

        var translation = (Translation)node;

        return translation.Value;
    }

    public string Namespace { get; set; }

    private static void MapTranslationGroup(IDictionary<string, string> result, TranslationGroup group)
    {
        foreach (var kvp in group.Children)
        {
            var node = kvp.Value;
            if (node is TranslationGroup subGroup)
                MapTranslationGroup(result, subGroup);
            else if (node is Translation translation)
                result.Add(translation.Name, translation.Value);
        }
    }
}
