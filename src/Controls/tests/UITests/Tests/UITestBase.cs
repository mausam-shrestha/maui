using System.Reflection;
using Microsoft.Maui.Appium;
using NUnit.Framework;
using NUnit.Framework.Interfaces;
using OpenQA.Selenium;
using TestUtils.Appium.UITests;
using VisualTestUtils;
using VisualTestUtils.MagickNet;

namespace Microsoft.Maui.AppiumTests
{
#if ANDROID
	[TestFixture(TestDevice.Android)]
#elif IOSUITEST
	[TestFixture(TestDevice.iOS)]
#elif MACUITEST
	[TestFixture(TestDevice.Mac)]
#elif WINTEST
	[TestFixture(TestDevice.Windows)]
#else
	[TestFixture(TestDevice.iOS)]
	[TestFixture(TestDevice.Mac)]
	[TestFixture(TestDevice.Windows)]
	[TestFixture(TestDevice.Android)]
#endif
	public class UITestBase : UITestContextTestBase
	{
		readonly TestDevice _testDevice;
		readonly VisualRegressionTester _visualRegressionTester;

		public UITestBase(TestDevice device)
		{
			_testDevice = device;

			string assemblyDirectory = Path.GetDirectoryName(System.AppDomain.CurrentDomain.BaseDirectory)!;
			string projectRootDirectory = Path.GetFullPath(Path.Combine(assemblyDirectory, "..\\..\\..\\.."));
			_visualRegressionTester = new VisualRegressionTester(testRootDirectory: projectRootDirectory,
				visualComparer: new MagickNetVisualComparer(),
				visualDiffGenerator: new MagickNetVisualDiffGenerator(),
				ciArtifactsDirectory: Environment.GetEnvironmentVariable("BUILD_ARTIFACTSTAGINGDIRECTORY"));
		}

		protected virtual void FixtureSetup() { }

		protected virtual void FixtureTeardown() { }

		[TearDown]
		public void UITestBaseTearDown()
		{
			var testOutcome = TestContext.CurrentContext.Result.Outcome;
			if (testOutcome == ResultState.Error ||
				testOutcome == ResultState.Failure)
			{
				var logDir = (Path.GetDirectoryName(Environment.GetEnvironmentVariable("APPIUM_LOG_FILE")) ?? Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location))!;

				_ = App.Screenshot(Path.Combine(logDir, $"{TestContext.CurrentContext.Test.MethodName}-ScreenShot"));

				if (App is IApp2 app2)
				{
					var pageSource = app2.ElementTree;
					File.WriteAllText(Path.Combine(logDir, $"{TestContext.CurrentContext.Test.MethodName}-PageSource.txt"), pageSource);
				}
			}
		}

		[OneTimeSetUp]
		public void OneTimeSetup()
		{
			InitialSetup(TestContextSetupFixture.TestContext);
			FixtureSetup();
		}

		[OneTimeTearDown()]
		public void OneTimeTearDown()
		{
			FixtureTeardown();
		}

		public override TestConfig GetTestConfig()
		{
			var appProjectFolder = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!, "..\\..\\..\\..\\..\\samples\\Controls.Sample.UITests");
			var appProjectPath = Path.Combine(appProjectFolder, "Controls.Sample.UITests.csproj");
			var testConfig = new TestConfig(_testDevice, "com.microsoft.maui.uitests")
			{
				BundleId = "com.microsoft.maui.uitests",
				AppProjectPath = appProjectPath
			};
			var windowsExe = "Controls.Sample.UITests.exe";
			var windoesExePath = Path.Combine(appProjectFolder, $"bin\\{testConfig.Configuration}\\{testConfig.FrameworkVersion}-windows10.0.20348\\win10-x64\\{windowsExe}");

			switch (_testDevice)
			{
				case TestDevice.iOS:
					testConfig.DeviceName = "iPhone X";
					testConfig.PlatformVersion = Environment.GetEnvironmentVariable("IOS_PLATFORM_VERSION") ?? "14.4";
					testConfig.Udid = Environment.GetEnvironmentVariable("IOS_SIMULATOR_UDID") ?? "";
					break;
				case TestDevice.Windows:
					testConfig.DeviceName = "WindowsPC";
					testConfig.AppPath = string.IsNullOrEmpty(Environment.GetEnvironmentVariable("WINDOWS_APP_PATH"))
						? windoesExePath
						: Environment.GetEnvironmentVariable("WINDOWS_APP_PATH");
					break;
			}

			return testConfig;
		}

		public void VerifyScreenshot(string? name = null)
		{
			if (name == null)
				name = TestContext.CurrentContext.Test.MethodName;

			IApp2? app = App as IApp2;
			if (app is null)
				throw new InvalidOperationException("App is not an IApp2");

			byte[] screenshotPngBytes = app.Screenshot();
			if (screenshotPngBytes is null)
				throw new InvalidOperationException("Failed to get screenshot");

			var actualImage = new ImageSnapshot(screenshotPngBytes, ImageSnapshotFormat.PNG);

			string platform = _testDevice switch
			{
				TestDevice.Android => "android",
				TestDevice.iOS => "ios",
				TestDevice.Mac => "mac",
				TestDevice.Windows => "windows",
				_ => throw new NotImplementedException($"Unknown device type {_testDevice}"),
			};

			_visualRegressionTester.VerifyMatchesSnapshot(name!, actualImage, environmentName: platform);
		}
	}
}
