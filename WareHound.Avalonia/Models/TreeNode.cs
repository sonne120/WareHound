using System.Collections.ObjectModel;

namespace WareHound.Avalonia.Models;

public class TreeNode
{
    public string Text { get; set; }
    public ObservableCollection<TreeNode> Children { get; } = new();

    public TreeNode(string text)
    {
        Text = text;
    }

    public void AddChild(string text)
    {
        Children.Add(new TreeNode(text));
    }
}
