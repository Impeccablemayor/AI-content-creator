namespace Api.Application.Publishing;

/// <summary>
/// Builds the text sent to a platform from a content post.
/// </summary>
/// <remarks>
/// Deterministic on purpose: the same post always produces the same text, so a
/// publish is reproducible and testable. Parts are joined with a blank line and
/// empty parts are dropped, so a post with no call to action does not gain stray
/// whitespace.
/// </remarks>
public static class SocialPostComposer
{
    public static string Compose(
        string content,
        string callToAction,
        IReadOnlyList<string> hashtags)
    {
        var blocks = new List<string>(3);

        AddIfPresent(blocks, content);
        AddIfPresent(blocks, callToAction);

        var tags = (hashtags ?? [])
            .Select(tag => tag?.Trim())
            .Where(tag => !string.IsNullOrWhiteSpace(tag))
            .Select(tag => tag!.StartsWith('#') ? tag : $"#{tag}")
            .ToList();
        if (tags.Count > 0)
        {
            blocks.Add(string.Join(' ', tags));
        }

        return string.Join("\n\n", blocks);
    }

    private static void AddIfPresent(List<string> blocks, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            blocks.Add(value.Trim());
        }
    }
}
