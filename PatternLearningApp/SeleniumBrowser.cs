using System;
using System.Collections.Generic;
using System.Linq;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Remote;
using OpenQA.Selenium.Support.UI;

namespace PatternLearningApp
{
    // Lightweight wrapper that encapsulates an IWebDriver instance
    public class SeleniumBrowser : IDisposable
    {
        private readonly IWebDriver _driver;

        public SeleniumBrowser(IWebDriver driver)
        {
            _driver = driver ?? throw new ArgumentNullException(nameof(driver));
        }

        public IWebDriver Driver => _driver;

        public void Navigate(string url)
        {
            _driver.Navigate().GoToUrl(url);
        }

        public IWebElement FindByCss(string selector)
        {
            return _driver.FindElement(By.CssSelector(selector));
        }

        public IReadOnlyCollection<IWebElement> FindAllByCss(string selector)
        {
            return _driver.FindElements(By.CssSelector(selector));
        }

        public IWebElement FindElement (By by)
        {
            return _driver.FindElement(by);
        }

        public IWebElement WaitUntilVisisble(TimeSpan timeSpan, By by)
        {
            var wait = new WebDriverWait(_driver, timeSpan);
            // Wait until an element matching the selector exists and is displayed
            return wait.Until(drv =>
            {
                var elements = drv.FindElements(by);
                var el = elements.FirstOrDefault(e => e.Displayed);
                return el;
            });
        }
        public string Title => _driver.Title;

        public void Quit()
        {
            try
            {
                _driver.Quit();
            }
            catch
            {
                // swallow - best effort
            }
        }

        public void Dispose()
        {
            try
            {
                _driver.Dispose();
            }
            catch
            {
                // ignore dispose errors
            }
        }
    }

    public static class SeleniumBrowserFactory
    {
        // Create a local ChromeDriver-backed SeleniumBrowser.
        // Requires the ChromeDriver binary to be available (e.g. via Selenium.WebDriver.ChromeDriver package or in PATH).
        public static SeleniumBrowser CreateLocalChrome(Action<ChromeOptions>? configure = null)
        {
            var options = new ChromeOptions();
            configure?.Invoke(options);

            var service = ChromeDriverService.CreateDefaultService();
            service.HideCommandPromptWindow = true;

            IWebDriver driver = new ChromeDriver(service, options);
            return new SeleniumBrowser(driver);
        }

        // Create a RemoteWebDriver that talks to a Selenium Grid / remote endpoint.
        // Example: CreateRemote(new Uri("http://localhost:4444/wd/hub"));
        public static SeleniumBrowser CreateRemote(Uri gridUri, Action<DriverOptions>? configure = null)
        {
            if (gridUri == null) throw new ArgumentNullException(nameof(gridUri));

            var options = new ChromeOptions();
            configure?.Invoke(options);

            // RemoteWebDriver accepts DriverOptions in recent Selenium versions
            IWebDriver driver = new RemoteWebDriver(gridUri, options);
            return new SeleniumBrowser(driver);
        }
    }
}
