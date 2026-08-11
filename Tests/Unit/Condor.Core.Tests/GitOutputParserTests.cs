using Condor.Core.Project;

namespace Condor.Core.Tests;

public class GitOutputParserTests
{
    [Fact]
    public void IsInsideWorkTree_ReconoceTrueYFalse()
    {
        Assert.True(GitOutputParser.IsInsideWorkTree("true"));
        Assert.True(GitOutputParser.IsInsideWorkTree(" true\n"));
        Assert.False(GitOutputParser.IsInsideWorkTree("false"));
        Assert.False(GitOutputParser.IsInsideWorkTree(null));
        Assert.False(GitOutputParser.IsInsideWorkTree(""));
    }

    [Fact]
    public void ParseBranch_LimpiaYSinValorDevuelveNull()
    {
        Assert.Equal("main", GitOutputParser.ParseBranch("main\r\n"));
        Assert.Null(GitOutputParser.ParseBranch(""));
        Assert.Null(GitOutputParser.ParseBranch(null));
        Assert.Null(GitOutputParser.ParseBranch("   "));
    }

    [Fact]
    public void IsDirty_SoloConSalida()
    {
        Assert.True(GitOutputParser.IsDirty(" M archivo.txt"));
        Assert.False(GitOutputParser.IsDirty(""));
        Assert.False(GitOutputParser.IsDirty(null));
        Assert.False(GitOutputParser.IsDirty("   "));
    }

    [Fact]
    public void ParseLog_CapturaHashYAsuntoYRecorta()
    {
        var subject = new string('x', 90);
        var output = "abcdefgh1|" + subject + "\n12345678|asunto corto\n";

        var commits = GitOutputParser.ParseLog(output, 5, 8, 80);

        Assert.Equal(2, commits.Count);
        Assert.Equal("abcdefgh", commits[0].Hash);
        Assert.Equal(80, commits[0].Subject.Length);
        Assert.Equal("12345678", commits[1].Hash);
        Assert.Equal("asunto corto", commits[1].Subject);
    }

    [Fact]
    public void ParseLog_AsuntoConPipes_SoloDivideEnElPrimero()
    {
        var commits = GitOutputParser.ParseLog("abc12345|prueba|con|pipes\n", 5, 8, 80);

        var commit = Assert.Single(commits);
        Assert.Equal("abc12345", commit.Hash);
        Assert.Equal("prueba|con|pipes", commit.Subject);
    }

    [Fact]
    public void ParseLog_RespetaLimiteDeCambios()
    {
        var output = string.Join("\n", Enumerable.Range(0, 10).Select(i => "hash" + i + "|tema " + i));

        var commits = GitOutputParser.ParseLog(output, 5, 8, 80);

        Assert.Equal(5, commits.Count);
    }

    [Fact]
    public void ParseLog_NullDevuelveVacio()
    {
        Assert.Empty(GitOutputParser.ParseLog(null, 5, 8, 80));
    }
}