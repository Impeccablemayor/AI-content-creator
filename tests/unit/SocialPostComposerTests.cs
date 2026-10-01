using Api.Application.Publishing;
using Xunit;

namespace Api.Tests;

public sealed class SocialPostComposerTests
{
    [Fact]
    public void Content_call_to_action_and_hashtags_are_joined_in_order()
    {
        var text = SocialPostComposer.Compose("Main content", "Read more", ["AI", "Growth"]);

        Assert.Equal("Main content\n\nRead more\n\n#AI #Growth", text);
    }

    [Fact]
    public void An_existing_hash_is_not_doubled()
    {
        Assert.Equal(
            "Body\n\n#AI",
            SocialPostComposer.Compose("Body", string.Empty, ["#AI"]));
    }

    [Fact]
    public void A_post_with_no_call_to_action_has_no_gap_where_it_would_be()
    {
        var text = SocialPostComposer.Compose("Main content", string.Empty, ["AI"]);

        Assert.Equal("Main content\n\n#AI", text);
    }

    [Fact]
    public void A_post_with_no_hashtags_ends_with_the_call_to_action()
    {
        Assert.Equal(
            "Main content\n\nRead more",
            SocialPostComposer.Compose("Main content", "Read more", []));
    }

    [Fact]
    public void A_post_with_only_content_produces_only_content()
    {
        Assert.Equal("Main content", SocialPostComposer.Compose("Main content", "  ", []));
    }

    [Fact]
    public void Surrounding_whitespace_is_trimmed_from_every_part()
    {
        var text = SocialPostComposer.Compose("  Main content  ", "\nRead more\n", ["  AI  "]);

        Assert.Equal("Main content\n\nRead more\n\n#AI", text);
    }

    /// <summary>
    /// The same post must always produce the same text, otherwise a retry is no
    /// longer the same publish and results are hard to reason about.
    /// </summary>
    [Fact]
    public void Composing_the_same_post_twice_is_identical()
    {
        var first = SocialPostComposer.Compose("Main content", "Read more", ["AI", "Growth"]);
        var second = SocialPostComposer.Compose("Main content", "Read more", ["AI", "Growth"]);

        Assert.Equal(first, second);
    }
}
