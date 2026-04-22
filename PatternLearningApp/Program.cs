using System;
using System.Collections.Generic;
using OpenQA.Selenium;
using PatternLearningApp;
using AppLogLevel = PatternLearningApp.LogLevel;

var email = Environment.GetEnvironmentVariable("PostCrossing_Email") ?? string.Empty;
var password = Environment.GetEnvironmentVariable("PostCrossing_Password") ?? string.Empty;
const string baseUrl = "https://postcrossing.com/";

ConfigureLogger();

var requestId = Guid.NewGuid().ToString("N");
using var scope = Logger.Instance.BeginScope(new Dictionary<string, object?>
{
    ["requestId"] = requestId,
    ["userId"] = string.IsNullOrEmpty(email) ? null : email
});

Logger.Instance.Debug("Starting browser and navigation");
var browser = SeleniumBrowserFactory.CreateLocalChrome();
try
{
    NavigateToSiteAndLogin(browser, baseUrl, email, password);

    var postcardId = PromptPostcardId();
    if (!string.IsNullOrWhiteSpace(postcardId))
    {
        Logger.Instance.Info($"Navigiere zu ID {postcardId}");
        browser.Navigate($"https://www.postcrossing.com/postcards/{postcardId}");

        // wait for page specific element and read sender
        browser.WaitUntilVisisble(TimeSpan.FromSeconds(10), By.XPath("//*[@itemprop='alternateName']"));
        var sender = browser.FindElement(By.XPath("//div[@class='details-box sender']"));
        Logger.Instance.Debug("Sender-Details gefunden");
    }
}
finally
{
    browser.Quit();
    Logger.Instance.Debug("Browser beendet");
}

// -- Local helper functions -------------------------------------------------

void ConfigureLogger()
{
    // Configure logger: Debug level and console sink
    Logger.Instance.UpdateConfiguration(cfg =>
    {
        cfg.MinimumLevel = AppLogLevel.Debug;
        cfg.Sinks.Clear();
        cfg.Sinks.Add(new ConsoleLogSink());
    });
}

void NavigateToSiteAndLogin(SeleniumBrowser browser, string url, string email, string password)
{
    Logger.Instance.Debug($"Navigiere zu \"{url}\" ...");
    browser.Navigate(url);
    Logger.Instance.Debug("Navigation abgeschlossen");

    ClickLogin(browser);
    AcceptConsentIfPresent(browser);
    FillCredentialsAndSubmit(browser, email, password);
    WaitForLoginConfirmation(browser);
}

void ClickLogin(SeleniumBrowser browser)
{
    string xpath = XPathBuilder.RootDescendant("button").ContainsText("LOG IN").Build();
    Logger.Instance.Debug($"Login-Button XPath: {xpath}");
    var loginButton = browser.FindElement(By.XPath(xpath));
    Logger.Instance.Debug("Login-Button gefunden, klicke...");
    loginButton.Click();
}

void AcceptConsentIfPresent(SeleniumBrowser browser)
{
    try
    {
        Logger.Instance.Debug("Warte auf Consent-Button (bis 10s)");
        browser.WaitUntilVisisble(TimeSpan.FromSeconds(10), By.XPath("//button[contains(.,'Consent')]"))
               .Click();
        Logger.Instance.Debug("Consent-Button geklickt");
    }
    catch
    {
        Logger.Instance.Debug("Kein Consent-Button gefunden oder Klick fehlgeschlagen");
    }
}

void FillCredentialsAndSubmit(SeleniumBrowser browser, string email, string password)
{
    Logger.Instance.Debug("Warte auf Login-Formular");
    var usernameInput = browser.WaitUntilVisisble(TimeSpan.FromSeconds(10), By.XPath(XPathBuilder.RootDescendant("input").AttributeEquals("id", "username").Build()));
    Logger.Instance.Debug("Login Seite erkannt.");
    usernameInput.SendKeys(email);
    Logger.Instance.Debug("E-Mail eingegeben (Passwort nicht geloggt)");

    var passwordInput = browser.FindElement(By.Id("password"));
    passwordInput.SendKeys(password);
    Logger.Instance.Debug("Passwort eingegeben");

    browser.FindElement(By.Id("loginButton")).Click();
    Logger.Instance.Debug("Login-Button geklickt");
}

void WaitForLoginConfirmation(SeleniumBrowser browser)
{
    Logger.Instance.Debug("Warte auf Bestätigung (meter-header)");
    browser.WaitUntilVisisble(TimeSpan.FromSeconds(10), By.XPath(XPathBuilder.RootDescendant("div").AttributeEquals("class", "meter-header").Build()));
    Logger.Instance.Info("Login-Vorgang abgeschlossen");
}

string PromptPostcardId()
{
    Console.Write("Postkarten-ID eingeben:");
    return Console.ReadLine() ?? string.Empty;
}
