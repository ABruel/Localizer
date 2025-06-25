namespace Localizer.TranslationTrees;

using System.Collections.Concurrent;

public class TranslationGroup : TranslationTreeNode
{
    public TranslationGroup(string name, ConcurrentDictionary<string, TranslationTreeNode> children)
        : base(name)
    {
        Children = children;
    }

    public ConcurrentDictionary<string, TranslationTreeNode> Children { get; }
}