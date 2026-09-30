using System.Text;
using Api.Domain.Entities;

namespace Api.Application.ContentEvaluation;

public sealed record ContentRepetitionResult(
    decimal Similarity,
    Guid? SimilarToContentPostId)
{
    public bool IsRepetitive { get; init; }

    public decimal Threshold { get; init; }
}

public sealed class ContentRepetitionDetector
{
    public ContentRepetitionResult Evaluate(
        string content,
        IEnumerable<ContentPost> candidates,
        decimal threshold)
    {
        var tokens = Tokenize(content);
        var best = 0m;
        Guid? bestId = null;

        foreach (var candidate in candidates)
        {
            var similarity = Similarity(tokens, Tokenize(candidate.Content));
            if (similarity <= best)
            {
                continue;
            }

            best = similarity;
            bestId = candidate.Id;
        }

        return new ContentRepetitionResult(best, bestId)
        {
            IsRepetitive = best > 0m && best >= threshold,
            Threshold = threshold
        };
    }

    public static HashSet<string> Tokenize(string? value)
    {
        var tokens = new HashSet<string>(StringComparer.Ordinal);
        if (string.IsNullOrWhiteSpace(value))
        {
            return tokens;
        }

        var current = new StringBuilder();
        foreach (var rune in value.EnumerateRunes())
        {
            if (!Rune.IsLetterOrDigit(rune))
            {
                Flush(current, tokens);
                continue;
            }

            current.Append(Rune.ToLowerInvariant(rune));
        }

        Flush(current, tokens);
        return tokens;
    }

    private static void Flush(StringBuilder current, HashSet<string> tokens)
    {
        if (current.Length >= MinTokenLength)
        {
            tokens.Add(current.ToString());
        }

        current.Clear();
    }

    private static decimal Similarity(HashSet<string> left, HashSet<string> right)
    {
        if (left.Count == 0 || right.Count == 0)
        {
            return 0m;
        }

        var intersection = 0;
        foreach (var token in left)
        {
            if (right.Contains(token))
            {
                intersection++;
            }
        }

        var union = left.Count + right.Count - intersection;
        return union == 0 ? 0m : Math.Round(intersection / (decimal)union, 4);
    }

    private const int MinTokenLength = 3;
}
