﻿using System.Collections.Generic;

namespace Localizer.TranslationTrees;

public class HierarchicalTranslationTreeBuilder : ITranslationTreeBuilder
{
    private readonly Dictionary<string, Dictionary<string, object>> _groups = new();

    private readonly Dictionary<string, object> _root = new();

    public void AddTranslation(string key, string text)
    {
        var parts = new string[] { key };

        var parentGroup = _root;

        if (parts.Length > 1)
        {
            var currentPath = "";

            for (var i = 0; i < parts.Length - 1; i++)
            {
                var part = parts[i];

                if (i > 0)
                    currentPath += "#";
                currentPath += part;

                if (_groups.ContainsKey(currentPath))
                {
                    parentGroup = _groups[currentPath];
                }
                else
                {
                    var group = new Dictionary<string, object>();
                    parentGroup.Add(part, group);
                    parentGroup = group;
                    _groups.Add(currentPath, group);
                }
            }
        }

        parentGroup.Add(parts[parts.Length - 1], text);
    }

    public ITranslationTree Build()
    {
        var root = BuildNode("", _root);

        return new TranslationTree(root);
    }

    public string Namespace { get; set; }

    private TranslationGroup BuildNode(string name, Dictionary<string, object> parentNode)
    {
        var nodes = new System.Collections.Concurrent.ConcurrentDictionary<string, TranslationTreeNode>();

        foreach (var node in parentNode)
        {
            if (node.Value is Dictionary<string, object> childGroup)
            {
                var group = BuildNode(node.Key, childGroup);

                nodes.AddOrUpdate(group.Name, (_) => group, (_, _) => group);
            }

            else
            {
                var entry = new Translation(node.Key, node.Value as string);

                nodes.AddOrUpdate(entry.Name, (_) => entry, (_, _) => entry);
            }
        }

        return new TranslationGroup(name, nodes);
    }
}
