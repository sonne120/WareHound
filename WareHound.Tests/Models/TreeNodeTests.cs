using FluentAssertions;
using WareHound.UI.Models;

namespace WareHound.Tests.Models;

public class TreeNodeTests
{
    [Fact]
    public void TreeNode_ShouldInitializeWithEmptyChildren()
    {
        // Arrange & Act
        var node = new TreeNode("Test");

        // Assert
        node.Children.Should().NotBeNull();
        node.Children.Should().BeEmpty();
    }

    [Fact]
    public void TreeNode_ShouldSetTextCorrectly()
    {
        // Arrange
        var node = new TreeNode("Test Node");

        // Act & Assert
        node.Text.Should().Be("Test Node");
    }

    [Fact]
    public void TreeNode_Text_ShouldBeSettable()
    {
        // Arrange
        var node = new TreeNode("Initial");

        // Act
        node.Text = "Updated";

        // Assert
        node.Text.Should().Be("Updated");
    }

    [Fact]
    public void TreeNode_AddChild_ShouldAddChildCorrectly()
    {
        // Arrange
        var parent = new TreeNode("Parent");

        // Act
        var child = parent.AddChild("Child");

        // Assert
        parent.Children.Should().HaveCount(1);
        parent.Children.Should().Contain(child);
        child.Text.Should().Be("Child");
    }

    [Fact]
    public void TreeNode_AddChild_ShouldReturnChildNode()
    {
        // Arrange
        var parent = new TreeNode("Parent");

        // Act
        var child = parent.AddChild("Child Text");

        // Assert
        child.Should().NotBeNull();
        child.Text.Should().Be("Child Text");
    }

    [Fact]
    public void TreeNode_ShouldSupportNestedChildren()
    {
        // Arrange
        var root = new TreeNode("Root");

        // Act
        var level1 = root.AddChild("Level1");
        var level2 = level1.AddChild("Level2");

        // Assert
        root.Children.First().Children.First().Text.Should().Be("Level2");
    }

    [Fact]
    public void TreeNode_ShouldSupportMultipleChildren()
    {
        // Arrange
        var parent = new TreeNode("Parent");

        // Act
        parent.AddChild("Child1");
        parent.AddChild("Child2");
        parent.AddChild("Child3");

        // Assert
        parent.Children.Should().HaveCount(3);
    }

    [Fact]
    public void TreeNode_Children_ShouldBeObservableCollection()
    {
        // Arrange
        var node = new TreeNode("Test");
        var collectionChangedCount = 0;
        node.Children.CollectionChanged += (_, _) => collectionChangedCount++;

        // Act
        node.AddChild("Child1");
        node.AddChild("Child2");

        // Assert
        collectionChangedCount.Should().Be(2);
    }
}
