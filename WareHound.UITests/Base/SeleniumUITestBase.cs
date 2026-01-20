using OpenQA.Selenium;
using OpenQA.Selenium.Appium;
using OpenQA.Selenium.Appium.Windows;
using OpenQA.Selenium.Support.UI;

namespace WareHound.UITests.Base;

public abstract class SeleniumUITestBase : IDisposable
{
    protected WindowsDriver? Driver { get; private set; }
    protected const string WinAppDriverUrl = "http://127.0.0.1:4723";
    
    private static readonly string DefaultAppPath = GetDefaultAppPath();

    private static string GetDefaultAppPath()
    {
        var basePath = AppDomain.CurrentDomain.BaseDirectory;
        var solutionDir = Path.GetFullPath(Path.Combine(basePath, "..", "..", "..", ".."));
        return Path.Combine(solutionDir, "WareHound.UI", "bin", "x64", "Debug", "net8.0-windows", "win-x64", "WareHound.UI.exe");
    }

    protected void InitializeDriver(string? appPath = null)
    {
        var path = appPath ?? DefaultAppPath;

        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Application not found at: {path}. Please build the WareHound.UI project first.");
        }

        var options = new AppiumOptions();
        options.AddAdditionalAppiumOption("app", path);
        options.AddAdditionalAppiumOption("deviceName", "WindowsPC");
        options.PlatformName = "Windows";

        try
        {
            Driver = new WindowsDriver(new Uri(WinAppDriverUrl), options);
            Driver.Manage().Timeouts().ImplicitWait = TimeSpan.FromSeconds(5);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                "Failed to initialize Windows Driver. Make sure WinAppDriver is running. " +
                "Download from: https://github.com/microsoft/WinAppDriver/releases", ex);
        }
    }

    protected IWebElement? FindElementById(string automationId)
    {
        try
        {
            return Driver?.FindElement(By.Id(automationId));
        }
        catch (NoSuchElementException)
        {
            return null;
        }
    }

    protected IWebElement? FindElementByName(string name)
    {
        try
        {
            return Driver?.FindElement(By.Name(name));
        }
        catch (NoSuchElementException)
        {
            return null;
        }
    }

    protected IWebElement? FindElementByClassName(string className)
    {
        try
        {
            return Driver?.FindElement(By.ClassName(className));
        }
        catch (NoSuchElementException)
        {
            return null;
        }
    }

    protected IWebElement? FindElementByXPath(string xpath)
    {
        try
        {
            return Driver?.FindElement(By.XPath(xpath));
        }
        catch (NoSuchElementException)
        {
            return null;
        }
    }

    protected IReadOnlyCollection<IWebElement> FindElementsByClassName(string className)
    {
        try
        {
            return Driver?.FindElements(By.ClassName(className)) ?? (IReadOnlyCollection<IWebElement>)Array.Empty<IWebElement>();
        }
        catch (NoSuchElementException)
        {
            return Array.Empty<IWebElement>();
        }
    }

    protected void WaitForElement(string automationId, TimeSpan timeout)
    {
        if (Driver == null) return;

        var wait = new WebDriverWait(Driver, timeout);
        wait.Until(d => d.FindElement(By.Id(automationId)));
    }

    protected void WaitForElementByName(string name, TimeSpan timeout)
    {
        if (Driver == null) return;

        var wait = new WebDriverWait(Driver, timeout);
        wait.Until(d => d.FindElement(By.Name(name)));
    }

    protected void ClickElement(string automationId)
    {
        var element = FindElementById(automationId);
        element?.Click();
    }

    protected void SendKeys(string automationId, string text)
    {
        var element = FindElementById(automationId);
        element?.SendKeys(text);
    }

    protected void ClearAndSendKeys(string automationId, string text)
    {
        var element = FindElementById(automationId);
        element?.Clear();
        element?.SendKeys(text);
    }

    protected string? GetElementText(string automationId)
    {
        var element = FindElementById(automationId);
        return element?.Text;
    }

    protected bool IsElementDisplayed(string automationId)
    {
        var element = FindElementById(automationId);
        return element?.Displayed ?? false;
    }

    protected bool IsElementEnabled(string automationId)
    {
        var element = FindElementById(automationId);
        return element?.Enabled ?? false;
    }

    protected void TakeScreenshot(string name)
    {
        if (Driver == null) return;

        var screenshotDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Screenshots");
        Directory.CreateDirectory(screenshotDir);

        var screenshot = ((ITakesScreenshot)Driver).GetScreenshot();
        var fileName = $"{name}_{DateTime.Now:yyyyMMdd_HHmmss}.png";
        screenshot.SaveAsFile(Path.Combine(screenshotDir, fileName));
    }

    public void Dispose()
    {
        Driver?.Quit();
        Driver?.Dispose();
    }
}
