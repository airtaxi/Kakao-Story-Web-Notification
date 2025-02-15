using OpenQA.Selenium.Edge;
using OpenQA.Selenium.Support.UI;
using OpenQA.Selenium;
using System.Net;
using SeleniumConfigurator;
using KakaoStoryWebNotification.Api;
using System.Drawing;
using System.IO;

namespace KakaoStoryWebNotification;

public static class LoginManager
{
	public static EdgeDriver SeleniumDriver = null;

	private static bool CheckIfElementExists(WebDriver driver, By by)
	{
		try
		{
			driver.FindElement(by);
			return true;
		}
		catch (NoSuchElementException)
		{
			return false;
		}
	}

	public static bool IsInLogin = false;
	public static IEnumerable<System.Net.Cookie> LoginWithSelenium(string email, string password, bool isHeadless = false)
	{
		IsInLogin = true;
		try
		{

			string driverPath = null;

			// Edge is preferred
			try { driverPath = Edge.GetDriverPath(); }
			catch (EdgeNotInstalledException) { }

			// If Edge is not installed, try Chrome
			if (driverPath == null)
			{
				try { driverPath = Chrome.GetDriverPath(); }
				catch (ChromeNotInstalledException) { }
			}

			// If both Edge and Chrome are not installed, throw an exception
			if (driverPath == null) throw new Exception("Edge or Chrome is not installed.");

			var service = EdgeDriverService.CreateDefaultService(driverPath);
			service.HideCommandPromptWindow = true;

			var options = new EdgeOptions();
			if (isHeadless) options.AddArgument("headless");

			// To avoid recaptcha, set the language to Korean
			options.AddArgument("--lang=ko");
			options.AddArgument("--accept-lang=ko");

			try { SeleniumDriver?.Close(); }
			catch (WebDriverException) { } // If the window is already closed, WebDriverException will be raised. Ignore it.
			finally { SeleniumDriver?.Dispose(); } // The object should be disposed to prevent memory leak.

			SeleniumDriver = new EdgeDriver(service, options);
			SeleniumDriver.Navigate().GoToUrl("https://accounts.kakao.com/login/?continue=https%3A%2F%2Fstory.kakao.com%2F&talk_login=&login_type=simple#login");

			try
			{
				var isNewLogin = CheckIfElementExists(SeleniumDriver, By.XPath("//*[@id=\"loginId--1\"]"));

				if (isNewLogin)
				{
					var emailBox = SeleniumDriver.FindElement(By.XPath("//*[@id=\"loginId--1\"]"));
					emailBox.SendKeys(email);

					var passwordBox = SeleniumDriver.FindElement(By.XPath("//*[@id=\"password--2\"]"));
					passwordBox.SendKeys(password);

					var checkBox = SeleniumDriver.FindElement(By.XPath("//*[@id=\"label-saveSignedIn\"]/span"));
					checkBox.Click();

					try
					{
						var loginButton = SeleniumDriver.FindElement(By.XPath("//*[@id=\"mainContent\"]/div/div/form/div[4]/button[1]"));
						loginButton.Click();
					}
					catch (NoSuchElementException)
					{
						var loginButton = SeleniumDriver.FindElement(By.XPath("//*[@id=\"mainContent\"]/div/div/form/div[5]/button[1]"));
						loginButton.Click();
					}
				}
				else
				{
					var emailBox = SeleniumDriver.FindElement(By.XPath("//*[@id=\"id_email_2\"]"));
					emailBox.SendKeys(email);

					var passwordBox = SeleniumDriver.FindElement(By.XPath("//*[@id=\"id_password_3\"]"));
					passwordBox.SendKeys(password);

					var checkBox = SeleniumDriver.FindElement(By.XPath("//*[@id=\"login-form\"]/fieldset/div[4]/div/label/span[1]"));
					checkBox.Click();

					var loginButton = SeleniumDriver.FindElement(By.XPath("//*[@id=\"login-form\"]/fieldset/div[8]/button[1]"));
					loginButton.Click();
				}

				var timeout = isHeadless ? TimeSpan.FromSeconds(10) : TimeSpan.FromDays(1);
				var wait = new WebDriverWait(SeleniumDriver, timeout);
				wait.Until(SeleniumExtras.WaitHelpers.ExpectedConditions.UrlToBe("https://story.kakao.com/"));
				var rawCookies = SeleniumDriver.Manage().Cookies.AllCookies;

				bool isSuccess = rawCookies.Any(x => x.Name == "_karmt");
				if (!isSuccess)

				{
					if (!isHeadless) return null;
					else return LoginWithSelenium(email, password, false);
				}

				//var result = SeleniumDriver.ExecuteScript("return Kakao.Auth.getAppKey();");
    //            var appKey = result.ToString();

				var cookies = new List<System.Net.Cookie>();
				var cookieContainer = new CookieContainer();

				foreach (var rawCookie in rawCookies)
				{
					var cookie = new System.Net.Cookie()
					{
						Name = rawCookie.Name,
						Domain = rawCookie.Domain,
						Path = rawCookie.Path,
						Value = rawCookie.Value
					};
					cookieContainer.Add(cookie);
					cookies.Add(cookie);
				}

				ApiHandler.Init(cookieContainer, cookies, null);
				return cookies;
			}
			catch (Exception)
			{
				if (!isHeadless) return null;
				else return LoginWithSelenium(email, password, false);
			}
			finally
			{
				SeleniumDriver?.Close();
				SeleniumDriver?.Dispose();
				SeleniumDriver = null;
			}
		}
		finally { IsInLogin = false; }
	}
}
