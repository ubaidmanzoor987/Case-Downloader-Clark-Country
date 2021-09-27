using NUnit.Framework;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Drawing;
using OpenQA.Selenium.Remote;
using DeathByCaptcha;
using Exception = System.Exception;

namespace ClarkCountryCaseDownloader
{

    [Parallelizable]
    public class Locate
    {
        public string DownloadPath;
        private ChromeDriverEx _d;
        public string UserDefinedPath;
        public string progress_message = "";
        ITakesScreenshot screenshotDriver;
        bool take_screenshot = false;
        int sleeptime = 3000;

        public class ChromeOptionsWithPrefs : ChromeOptions
        {
            public Dictionary<string, object> prefs { get; set; }
        }

        public class ChromeDriverEx : ChromeDriver
        {
            private const string SendChromeCommandWithResult = "sendChromeCommandWithResponse";
            private const string SendChromeCommandWithResultUrlTemplate = "/session/{sessionId}/chromium/send_command_and_get_result";

            public ChromeDriverEx(ChromeDriverService service, ChromeOptions options)
                : base(service, options)
            {
                CommandInfo commandInfoToAdd = new CommandInfo(CommandInfo.PostCommand, SendChromeCommandWithResultUrlTemplate);
                this.CommandExecutor.CommandInfoRepository.TryAddCommand(SendChromeCommandWithResult, commandInfoToAdd);
            }

            public object ExecuteChromeCommandWithResult(string commandName, Dictionary<string, object> commandParameters)
            {
                if (commandName == null)
                {
                    throw new ArgumentNullException("commandName", "commandName must not be null");
                }

                Dictionary<string, object> parameters = new Dictionary<string, object>();
                parameters["cmd"] = commandName;
                parameters["params"] = commandParameters;
                Response response = this.Execute(SendChromeCommandWithResult, parameters);
                return response.Value;
            }

            private Dictionary<string, object> EvaluateDevToolsScript(string scriptToEvaluate)
            {
                // This code is predicated on knowing the structure of the returned
                // object as the result. In this case, we know that the object returned
                // has a "result" property which contains the actual value of the evaluated
                // script, and we expect the value of that "result" property to be an object
                // with a "value" property. Moreover, we are assuming the result will be
                // an "object" type (which translates to a C# Dictionary<string, object>).
                Dictionary<string, object> parameters = new Dictionary<string, object>();
                parameters["returnByValue"] = true;
                parameters["expression"] = scriptToEvaluate;
                object evaluateResultObject = this.ExecuteChromeCommandWithResult("Runtime.evaluate", parameters);
                Dictionary<string, object> evaluateResultDictionary = evaluateResultObject as Dictionary<string, object>;
                Dictionary<string, object> evaluateResult = evaluateResultDictionary["result"] as Dictionary<string, object>;

                // If we wanted to make this actually robust, we'd check the "type" property
                // of the result object before blindly casting to a dictionary.
                Dictionary<string, object> evaluateValue = evaluateResult["value"] as Dictionary<string, object>;
                return evaluateValue;
            }
        }

        public bool InitializeDriver()
        {
            bool res;
            try
            {
                ChromeDriverService defaultService = ChromeDriverService.CreateDefaultService("./");
                defaultService.HideCommandPromptWindow = true;
                ChromeOptionsWithPrefs options = new ChromeOptionsWithPrefs();
                options.BinaryLocation = "C:\\Program Files (x86)\\Google\\Chrome\\Application\\chrome.exe";
                string val = "{\"version\":2,\"isGcpPromoDismissed\":false,\"selectedDestinationId\":\"Save as PDF\", \"recentDestinations\":[{\"id\":\"Save as PDF\", \"origin\":\"local\", \"account\":\"\"}]}";
                options.AddUserProfilePreference("printing.print_preview_sticky_settings.appState", val);
                options.AddUserProfilePreference("printing.default_destination_selection_rules", "{\"kind\":\"local\",\"namePattern\":\"Save as PDF\"}");
                options.AddArguments("--start-maximized");
                options.AddArgument("--kiosk-printing");
                options.AddArguments("--disable-gpu");
                options.AddArguments("--disable-extensions");
                options.AddArgument("headless");
                options.AddArgument("no-sandbox");
                options.AddArgument("test-type");
                options.AddArguments(new string[1]
                {
                    "disable-infobars"
                });

                string baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
                try
                {
                    _d = new ChromeDriverEx(defaultService, options);
                } catch (Exception ex1)
                {
                    Console.WriteLine(ex1);
                    options.BinaryLocation = "C:\\Program Files\\Google\\Chrome\\Application\\chrome.exe";
                    _d = new ChromeDriverEx(defaultService, options);
                }
                _d.Manage().Timeouts().ImplicitWait = TimeSpan.FromSeconds(5.0);
                screenshotDriver = _d as ITakesScreenshot;
                res = true;
            }
            catch (Exception ex)
            {
                Console.WriteLine("Exception in InitializeDriver", ex.Message);
                Thread.Sleep(sleeptime);
                res = false;
            }
            return res;
        }

        private void sleep(int time)
        {
            Thread.Sleep(time);
        }

        private void getScreenShot(string name)
        {
            if (this.take_screenshot)
            {
                Screenshot screenshot = screenshotDriver.GetScreenshot();
                screenshot.SaveAsFile(name + ".png", ScreenshotImageFormat.Png);
            }
        }

        private string getCaptchaBase64()
        {
            string base_64 = "";
            try
            {
                string img_base_64 = (this._d as IJavaScriptExecutor).ExecuteScript(@"
                var canvas = document.createElement('canvas');
                var ctx = canvas.getContext('2d');

                function getMaxSize(srcWidth, srcHeight, maxWidth, maxHeight) {
                    var widthScale = null;
                    var heightScale = null;

                    if (maxWidth != null)
                    {
                        widthScale = maxWidth / srcWidth;
                    }
                    if (maxHeight != null)
                    {
                        heightScale = maxHeight / srcHeight;
                    }

                    var ratio = Math.min(widthScale || heightScale, heightScale || widthScale);
                    return {
                        width: Math.round(srcWidth * ratio),
                        height: Math.round(srcHeight * ratio)
                    };
                }
                function getBase64FromImage(img, width, height) {
                    var size = getMaxSize(width, height, 400, 400)
                    canvas.width = size.width;
                    canvas.height = size.height;
                    ctx.fillStyle = 'white';
                    ctx.fillRect(0, 0, size.width, size.height);
                    ctx.drawImage(img, 0, 0, size.width, size.height);
                    return canvas.toDataURL('image/jpeg', 0.9);
                }
                var img = document.querySelector('#search_samplecaptcha_CaptchaImage');
                    return getBase64FromImage(img, img.width, img.height);
                ") as string;

                    base_64 = img_base_64.Split(',').Last();
                }
            catch (Exception ex)
            {
                Console.WriteLine("Exception in create captch base 64", ex.Message);
            }
            return base_64;
        }

        private bool SaveImage(string base64)
        {
            try
            {
                using (MemoryStream ms = new MemoryStream(Convert.FromBase64String(base64)))
                {
                    using (Bitmap bm2 = new Bitmap(ms))
                    {
                        bm2.Save("captcha.jpg");
                    }
                }
                return true;

            }
            catch (Exception ex)
            {
                Console.WriteLine("Exception in SaveImage Function", ex.Message);

            }

            return false;
        }

        public string captchaSolver(string base64)
        {
            string captcha_text = "";
            for (int i=1; i<=10; i++)
            {
                try
                {
                    Client client = (Client)new SocketClient("farukh", "webdir123R");
                    byte[] img = Convert.FromBase64String(base64);
                    Captcha captcha;
                    try
                    {
                        captcha = client.Decode(img, 10);
                    } catch (Exception ex1)
                    {
                        Console.WriteLine("Exception in captchaSolver", ex1.Message);
                        throw ex1;
                    }
                    if ( captcha != null && captcha.Solved && captcha.Correct)
                    {
                        Console.WriteLine("CAPTCHA {0}: {1}", captcha.Id, captcha.Text);
                        captcha_text = captcha.Text;
                    }
                    break;
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Exception in captchaSolver", ex.Message);
                    captchaSolver(base64);
                }
            }
            
            return captcha_text;
        }

        public bool saveCaptcha()
        {
            bool res = false;
            try
            {
                string img_base_64 = getCaptchaBase64();
                res = SaveImage(img_base_64);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Exception in saveCaptcha Function ", ex.Message);
            }
            return res;
        }

        public bool enterCaptchaText(string captchaText, string crossRefnumber)
        {
            bool res = false;
            try
            {
                IWebElement captchaField = FindElementIfExists(By.CssSelector("#CodeTextBox"));
                captchaField.SendKeys(captchaText);
                IWebElement crossRefRadio = FindElementIfExists(By.CssSelector("#CrossRefNumberOption"));
                crossRefRadio.Click();
                IWebElement crossRefField = FindElementIfExists(By.CssSelector("#CaseSearchValue"));
                crossRefField.SendKeys(crossRefnumber);
                getScreenShot("captchaScreen");
                IWebElement searchButton = FindElementIfExists(By.CssSelector("#SearchSubmit"));
                searchButton.Click();
                sleep(sleeptime);
                IWebElement incorrectCaptcha = null;
                try{
                    incorrectCaptcha = FindElementIfExists(By.XPath("//*[@id='MessageLabel']"));
                } catch (Exception ex1){
                    Console.WriteLine("Exception in Incorrect Captcha ", ex1);
                }
                if (incorrectCaptcha != null){
                    res = false;
                }else
                {
                    res = true;
                }
            }catch (Exception ex){
                Console.WriteLine("Exception in enterCaptchaText function", ex.Message);
            }
            return res;
        }

        public bool openingMainPage()
        {
            bool res = false;
            try
            {
                _d.Navigate().GoToUrl("https://www.clarkcountycourts.us/Anonymous/default.aspx");
                sleep(sleeptime);
                IWebElement lasVegasLink = FindElementIfExists(By.CssSelector("body > table > tbody > tr:nth-child(2) > td > table > tbody > tr:nth-child(1) > td:nth-child(2) > a:nth-child(14)"));
                lasVegasLink.Click();
                getScreenShot("lasvegas");
                sleep(sleeptime);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Exception in openingMainPage function", ex.Message);
            }
            return res;
        }

        public void ViewAndPayCriminalPage()
        {
            try
            {
                IWebElement viewAndPayCriminal = this.FindElementIfExists(By.CssSelector("body > table > tbody > tr:nth-child(2) > td > table > tbody > tr:nth-child(1) > td:nth-child(2) > a:nth-child(8)"));
                viewAndPayCriminal.Click();
                sleep(sleeptime);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Exception in ViewAndPayCriminalPage  Function", ex.Message);
            }
        }

        public bool LookingAndSolvingCaptcha(string cross_ref_number)
        {
            bool navigated = false ;
            try
            {
                for (int i = 1; i <= 5; i++)
                {
                    string img_base_64 = getCaptchaBase64();
                    string captcha_text = captchaSolver(img_base_64);
                    if (captcha_text.Length > 0)
                    {
                        navigated = enterCaptchaText(captcha_text, cross_ref_number);
                        if (navigated)
                        {
                            break;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Exception in LookingAndSolvingCaptcha  Function", ex.Message);
            }
            return navigated;
        }

        public void extractData(string cross_ref_number)
        {
            try
            {
                List<IWebElement> trsForLength = FindElementsIfExists(By.CssSelector("body > table:nth-child(5) > tbody > tr"));
                for (int trIndex = 0; trIndex < trsForLength.Count; trIndex ++)
                {
                    List<IWebElement> trs = FindElementsIfExists(By.CssSelector("body > table:nth-child(5) > tbody > tr"));
                    if (trIndex > 1)
                    {
                        var tr = trs[trIndex];
                        IWebElement caseLink = FindElementOnObject(By.CssSelector("td > a"), tr);
                        string file_name = caseLink.Text;
                        caseLink.Click();
                        sleep(sleeptime);
                        var commandParameters = new Dictionary<string, object>
                        {
                            { "format", "A4" },
                            { "scale", 0.9 },
                            { "behavior", "allow" },
                        };
                        var printOutput = (Dictionary<string, object>)_d.ExecuteChromeCommandWithResult("Page.printToPDF", commandParameters);
                        var pdf_data = Convert.FromBase64String(printOutput["data"] as string);
                        if (DownloadPath == null)
                        {
                            DownloadPath = Directory.GetCurrentDirectory() + "\\Cases Data\\";
                        }
                        var sub_directory = DownloadPath + "\\" + cross_ref_number;
                        if (Directory.Exists(sub_directory) == false) { 
                            new DirectoryInfo(sub_directory).Create();
                        }
                        string file = sub_directory + "\\" + file_name + ".pdf";
                        File.WriteAllBytes(file, pdf_data);
                        _d.Navigate().Back();
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Exception in extractData function", ex.Message);
            }
        }

        private IWebElement FindElementIfExists(By by)
        {
            try
            {
                ReadOnlyCollection<IWebElement> elements = _d.FindElements(by);
                return elements.Count >= 1 ? elements.First<IWebElement>() : (IWebElement)null;
            }
            catch
            {
                return null;
            }
        }

        private List<IWebElement> FindElementsIfExists(By by)
        {
            try
            {
                var webElements = _d.FindElements(by);
                return webElements.ToList();
            }
            catch
            {
                return null;
            }
        }

        private IWebElement FindElementOnObject(By by, IWebElement el)
        {
            try
            {
                var webElement = el.FindElement(by);
                return webElement;
            }
            catch
            {
                return null;
            }
        }

        [TearDown]
        public void Cleanup()
        {
            try
            {
                _d.Quit();
                _d = null;
            }
            catch (Exception ex)
            {
                Console.WriteLine("Exception in Cleanup function", ex);
            }
        }

    }
}
