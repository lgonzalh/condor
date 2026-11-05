using Condor.Infrastructure.Agent;

namespace Condor.Infrastructure.Tests;

public sealed class MultiAgentReviewPolicyTests
{
    [Theory]
    [InlineData("Solicito una revisión multiagente del cambio.")]
    [InlineData("Solicito una revision multiagente del cambio.")]
    [InlineData("Pido multi-agent review del cambio.")]
    public void IsRequested_ReconoceSolicitudExplicita(string intention)
    {
        Assert.True(MultiAgentReviewPolicy.IsRequested(intention));
    }

    [Fact]
    public void HasIndependentReviewers_RequiereRevisionGeneralYDeSeguridad()
    {
        Assert.True(MultiAgentReviewPolicy.HasIndependentReviewers(new[] { "reviewer", "security-reviewer" }));
        Assert.False(MultiAgentReviewPolicy.HasIndependentReviewers(new[] { "reviewer" }));
        Assert.False(MultiAgentReviewPolicy.HasIndependentReviewers(new[] { "reviewer", "reviewer" }));
    }
}
