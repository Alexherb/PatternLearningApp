using System;
using System.Collections.Generic;
using OpenQA.Selenium;
using PatternLearningApp;

var email = Environment.GetEnvironmentVariable("PostCrossing_Email") ?? string.Empty;
var password = Environment.GetEnvironmentVariable("PostCrossing_Password") ?? string.Empty;

// Configure logger: Debug level and console sink
Logger.Instance.UpdateConfiguration(cfg =>
{
    cfg.MinimumLevel = PatternLearningApp.LogLevel.Debug;
    cfg.Sinks.Clear();
    cfg.Sinks.Add(new ConsoleLogSink());
});

var requestId = Guid.NewGuid().ToString("N");
using var scope = Logger.Instance.BeginScope(new Dictionary<string, object?>
{
    ["requestId"] = requestId,
    ["userId"] = string.IsNullOrEmpty(email) ? null : email
});

Logger.Instance.Debug("Starting browser and navigation");
var browser = SeleniumBrowserFactory.CreateLocalChrome();
string url = "https://postcrossing.com/";
Logger.Instance.Debug($"Navigiere zu \"{url}\" ...");
browser.Navigate(url);
Logger.Instance.Debug("Navigation abgeschlossen");

string xpath = XPathBuilder.RootDescendant("button").ContainsText("LOG IN").Build();
Logger.Instance.Debug($"Login-Button XPath: {xpath}");
var loginButton = browser.FindElement(By.XPath(xpath));
Logger.Instance.Debug("Login-Button gefunden, klicke...");
loginButton.Click();

try
{
    Logger.Instance.Debug("Warte auf Consent-Button (bis 10s)");
    browser.WaitUntilVisisble(TimeSpan.FromSeconds(10), By.XPath("//button[contains(.,'Consent')]")).Click();
    Logger.Instance.Debug("Consent-Button geklickt");
}
catch
{
    Logger.Instance.Debug("Kein Consent-Button gefunden oder Klick fehlgeschlagen");
}

Logger.Instance.Debug("Warte auf Login-Formular");
var usernameInput = browser.WaitUntilVisisble(TimeSpan.FromSeconds(10), By.XPath(XPathBuilder.RootDescendant("input").AttributeEquals("id", "username").Build()));
Logger.Instance.Debug("Login Seite erkannt.");
usernameInput.SendKeys(email);
Logger.Instance.Debug("E-Mail eingegeben (um Inhalte zu schützen wurde das Passwort nicht geloggt)");

var passwordInput = browser.FindElement(By.Id("password"));
passwordInput.SendKeys(password);
Logger.Instance.Debug("Passwort eingegeben");

browser.FindElement(By.Id("loginButton")).Click();
Logger.Instance.Debug("Login-Button geklickt, warte auf Bestätigung (meter-header)");

var confirmation = browser.WaitUntilVisisble(TimeSpan.FromSeconds(10), By.XPath(XPathBuilder.RootDescendant("div").AttributeEquals("class", "meter-header").Build()));
Logger.Instance.Info("Login-Vorgang abgeschlossen");

browser.Quit();
Logger.Instance.Debug("Browser beendet");
