using Dograc.Core.Parsing;
using Xunit;

namespace Dograc.Core.Tests;

public sealed class CitationParserTests
{
    [Fact]
    public void ExtractUniqueCitations_DeduplicatesAndParsesKoreanFilenames()
    {
        var content =
            "사내 보안 규정에 따르면 외부 반출이 금지됩니다 [사내_정보보안_규정.pdf, p.3]. " +
            "또한 연차 사용은 사전 승인이 필요합니다 [근태 관리 지침.pdf, p.12] " +
            "(중복 참조 [사내_정보보안_규정.pdf, p.3]).";

        var citations = CitationParser.ExtractUniqueCitations(content);

        Assert.Equal(2, citations.Count);
        Assert.Equal("사내_정보보안_규정.pdf", citations[0].FileName);
        Assert.Equal(3, citations[0].PageNumber);
        Assert.Equal("p.3", citations[0].DisplayPage);
        Assert.Equal("근태 관리 지침.pdf", citations[1].FileName);
        Assert.Equal(12, citations[1].PageNumber);
    }

    [Fact]
    public void SplitTextWithCitations_PreservesSurroundingPlainTextAndOrder()
    {
        var text = "핵심 근거는 [policy.pdf, p.5]와 [guide.pdf, p.2]에 있습니다.";

        var segments = CitationParser.SplitTextWithCitations(text);

        Assert.Equal(5, segments.Count);
        Assert.False(segments[0].IsCitation);
        Assert.Equal("핵심 근거는 ", segments[0].Text);

        Assert.True(segments[1].IsCitation);
        Assert.Equal("policy.pdf", segments[1].Citation!.FileName);
        Assert.Equal(5, segments[1].Citation!.PageNumber);

        Assert.False(segments[2].IsCitation);
        Assert.Equal("와 ", segments[2].Text);

        Assert.True(segments[3].IsCitation);
        Assert.Equal("guide.pdf", segments[3].Citation!.FileName);
        Assert.Equal(2, segments[3].Citation!.PageNumber);

        Assert.False(segments[4].IsCitation);
        Assert.Equal("에 있습니다.", segments[4].Text);
    }

    [Fact]
    public void SplitTextWithCitations_HandlesPlainTextWithoutCitations()
    {
        var segments = CitationParser.SplitTextWithCitations("일반 답변 문장입니다.");

        var single = Assert.Single(segments);
        Assert.False(single.IsCitation);
        Assert.Equal("일반 답변 문장입니다.", single.Text);
    }
}
