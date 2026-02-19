using Xunit.v3;

namespace Ciderfy.Tests;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
internal sealed class IntegrationTestAttribute : Attribute, ITraitAttribute
{
    public IReadOnlyCollection<KeyValuePair<string, string>> GetTraits() =>
        [new("Category", "Integration")];
}
