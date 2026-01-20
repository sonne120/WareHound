using Xunit;

namespace WareHound.UITests.Tests;

[CollectionDefinition("UI Tests", DisableParallelization = true)]
public class UITestsCollection : ICollectionFixture<object>
{
}
