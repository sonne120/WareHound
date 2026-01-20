using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Capturing;
using FlaUI.UIA3;

namespace WareHound.UITests.Base;

public abstract class WpfUITestBase : IDisposable
{
    protected Application? App { get; private set; }
    protected UIA3Automation? Automation { get; private set; }
    protected Window? MainWindow { get; private set; }

    private static readonly string DefaultAppPath = GetDefaultAppPath();

    private static string GetDefaultAppPath()
    {
        // Get the path to the WareHound.UI executable
        var directory = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
        while (directory != null && !directory.GetFiles("*.sln").Any())
        {
            directory = directory.Parent;
        }
        var solutionDir = directory?.FullName ?? throw new Exception("Could not find solution directory.");

        // Try to find the executable in a few common locations
        var possiblePaths = new[]
        {
            Path.Combine(solutionDir, "WareHound.UI", "bin", "x64", "Debug", "net8.0-windows", "win-x64", "WareHound.UI.exe"),
            Path.Combine(solutionDir, "WareHound.UI", "bin", "Debug", "net8.0-windows", "win-x64", "WareHound.UI.exe"),
            Path.Combine(solutionDir, "WareHound.UI", "bin", "Debug", "net8.0-windows", "WareHound.UI.exe")
        };

        foreach (var path in possiblePaths)
        {
            if (File.Exists(path)) return path;
        }

        return possiblePaths[0];
    }

    protected void LaunchApplication(string? appPath = null)
    {
        var path = appPath ?? DefaultAppPath;
        
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Application not found at: {path}. Please build the WareHound.UI project first.");
        }

        Automation = new UIA3Automation();
        App = Application.Launch(path);
        
        // Wait for the main window to appear
        MainWindow = App.GetMainWindow(Automation, TimeSpan.FromSeconds(30));
    }

    protected AutomationElement? FindElementByAutomationId(string automationId)
    {
        return MainWindow?.FindFirstDescendant(cf => cf.ByAutomationId(automationId));
    }

    protected AutomationElement? FindElementByName(string name)
    {
        return MainWindow?.FindFirstDescendant(cf => cf.ByName(name));
    }

    protected AutomationElement? FindElementByClassName(string className)
    {
        return MainWindow?.FindFirstDescendant(cf => cf.ByClassName(className));
    }

    protected Button? FindButton(string automationIdOrName)
    {
        var element = FindElementByAutomationId(automationIdOrName) 
                      ?? FindElementByName(automationIdOrName);
        return element?.AsButton();
    }

    protected TextBox? FindTextBox(string automationIdOrName)
    {
        var element = FindElementByAutomationId(automationIdOrName) 
                      ?? FindElementByName(automationIdOrName);
        return element?.AsTextBox();
    }

    protected ComboBox? FindComboBox(string automationIdOrName)
    {
        var element = FindElementByAutomationId(automationIdOrName) 
                      ?? FindElementByName(automationIdOrName);
        return element?.AsComboBox();
    }

    protected AutomationElement? FindDataGrid(string automationIdOrName)
    {
        var element = FindElementByAutomationId(automationIdOrName) 
                      ?? FindElementByName(automationIdOrName);
        return element;
    }

    protected void WaitForElement(string automationId, TimeSpan timeout)
    {
        var endTime = DateTime.Now.Add(timeout);
        while (DateTime.Now < endTime)
        {
            var element = FindElementByAutomationId(automationId);
            if (element != null)
                return;
            Thread.Sleep(100);
        }
        throw new TimeoutException($"Element with AutomationId '{automationId}' not found within {timeout.TotalSeconds} seconds.");
    }

    protected void TakeScreenshot(string name)
    {
        if (MainWindow == null) return;

        var screenshotDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Screenshots");
        Directory.CreateDirectory(screenshotDir);
        
        var screenshot = Capture.Screen();
        var fileName = $"{name}_{DateTime.Now:yyyyMMdd_HHmmss}.png";
        screenshot.ToFile(Path.Combine(screenshotDir, fileName));
    }

    public void Dispose()
    {
        MainWindow = null;
        Automation?.Dispose();
        App?.Close();
        App?.Dispose();
    }
}
