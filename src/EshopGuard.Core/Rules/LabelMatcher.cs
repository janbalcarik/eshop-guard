namespace EshopGuard.Core.Rules;

/// <summary>
/// Finds label names in text as whole words, ignoring case, diacritics and punctuation:
/// "EU Ecolabel" matches "Nese ekoznačku EU Ecolabel." and the file name "eu-ecolabel.png".
/// </summary>
internal sealed class LabelMatcher(LabelConfiguration labels)
{
    private readonly Dictionary<string, List<string>> _lists = labels.Lists.ToDictionary(
        pair => pair.Key,
        pair => pair.Value.Select(Normalize).Where(item => item.Trim().Length > 0).ToList());

    private readonly List<(List<string> Names, string Note)> _notes = labels.Notes
        .Select(note => (note.Names.Select(Normalize).Where(name => name.Trim().Length > 0).ToList(), note.Note))
        .ToList();

    /// <summary>Remarks on the labels named in the text.</summary>
    public IEnumerable<string> NotesFor(string text)
    {
        var haystack = Normalize(text);
        return _notes.Where(n => n.Names.Any(name => haystack.Contains(name, StringComparison.Ordinal))).Select(n => n.Note);
    }

    public bool ContainsAny(string text, string listName)
    {
        if (!_lists.TryGetValue(listName, out var items))
        {
            return false;
        }

        var haystack = Normalize(text);
        return items.Any(item => haystack.Contains(item, StringComparison.Ordinal));
    }

    /// <summary>Lower-case ASCII words separated by single spaces, padded with a space on both sides.</summary>
    public static string Normalize(string text) =>
        " " + string.Join(' ', TextTools.Slugify(text).Split('-', StringSplitOptions.RemoveEmptyEntries)) + " ";
}
