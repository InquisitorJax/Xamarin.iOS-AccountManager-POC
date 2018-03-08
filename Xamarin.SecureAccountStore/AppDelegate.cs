using System;
using System.Linq;
using System.Threading.Tasks;
using Foundation;
using UIKit;
using Xamarin.SecureAccountStore.CustomCode;

namespace Xamarin.SecureAccountStore
{
	// The UIApplicationDelegate for the application. This class is responsible for launching the
	// User Interface of the application, as well as listening (and optionally responding) to
	// application events from iOS.
	[Register("AppDelegate")]
	public class AppDelegate : UIApplicationDelegate
	{
		// class-level declarations

		public override UIWindow Window
		{
			get;
			set;
		}

		public override bool FinishedLaunching(UIApplication application, NSDictionary launchOptions)
		{
			// Override point for customization after application launch.
			// If not required for your application you can safely delete this method

			//NOTE: !! XAuth serializes the Account into SecRecord.ValueData - whereas Tiny only serializes the Properties into SecRecord.ValueData.
			// The 2 stores are therefore NOT COMPATIBLE - and if you try fetch a XAuth record using Tiny, Json deserialization will FAIL SILENTLY!!!
			TinyInitializeDeviceIDAsync().ConfigureAwait(true);
			//XAuthInitializeDeviceIDAsync().ConfigureAwait(true);

			Console.WriteLine("DONE!");

			return true;
		}

		public override void OnResignActivation(UIApplication application)
		{
			// Invoked when the application is about to move from active to inactive state.
			// This can occur for certain types of temporary interruptions (such as an incoming phone call or SMS message)
			// or when the user quits the application and it begins the transition to the background state.
			// Games should use this method to pause the game.
		}

		public override void DidEnterBackground(UIApplication application)
		{
			// Use this method to release shared resources, save user data, invalidate timers and store the application state.
			// If your application supports background exection this method is called instead of WillTerminate when the user quits.
		}

		public override void WillEnterForeground(UIApplication application)
		{
			// Called as part of the transiton from background to active state.
			// Here you can undo many of the changes made on entering the background.
		}

		public override void OnActivated(UIApplication application)
		{
			// Restart any tasks that were paused (or not yet started) while the application was inactive.
			// If the application was previously in the background, optionally refresh the user interface.
		}

		public override void WillTerminate(UIApplication application)
		{
			// Called when the application is about to terminate. Save data, if needed. See also DidEnterBackground.
		}

		private async Task XAuthInitializeDeviceIDAsync()
		{
			var accountManager = new KeyChainAccountStore();

			string deviceIdentifier = null;
			var existingAccounts = await accountManager.FindAccountsForServiceAsync(DeviceIdentifierServiceID);
			Account deviceAccount = null;
			if (existingAccounts.Count > 0)
			{
				Console.WriteLine("EXISTING DEVICE ID FOUND :) ");

				deviceAccount = existingAccounts.First();
				deviceIdentifier = deviceAccount.Username;
				Console.WriteLine("FOUND DEVICE IDENTIFIER: " + deviceIdentifier);
			}
			else
			{
				deviceIdentifier = Guid.NewGuid().ToString();
				Console.WriteLine("SAVING NEW DEVICE ID: " + deviceIdentifier);
				deviceAccount = new Account
				{
					ServiceId = DeviceIdentifierServiceID,
					Username = deviceIdentifier
				};
				await accountManager.SaveAsync(deviceAccount, DeviceIdentifierServiceID);
				Console.WriteLine("SAVED DEVICE IDENTIFIER: " + deviceIdentifier);
			}
		}

		private const string DeviceIdentifierServiceID = "com.mycompanyname.appname.deviceid";
		private const string DeviceIdentifierServiceID2 = "com.mycompanyname.appname.deviceid2";

		private async Task TinyInitializeDeviceIDAsync()
		{
			
			var accountManager = new TinyiOSAccountManager();

			string deviceIdentifier = null;
			bool existingAccount = await accountManager.Exists(DeviceIdentifierServiceID2);
			Account deviceAccount = null;
			if (existingAccount)
			{
				Console.WriteLine("EXISTING DEVICE ID FOUND :) ");
				//BUG: fails silently on get - _deviceIdentifier is never set.
				deviceAccount = await accountManager.Get(DeviceIdentifierServiceID2);
				deviceIdentifier = deviceAccount.Username;
				Console.WriteLine("FOUND DEVICE IDENTIFIER: " + deviceIdentifier);
			}
			else
			{
				deviceIdentifier = Guid.NewGuid().ToString();
				Console.WriteLine("SAVING NEW DEVICE ID: " + deviceIdentifier);
				deviceAccount = new Account
				{
					ServiceId = DeviceIdentifierServiceID2,
					Username = deviceIdentifier
				};
				await accountManager.Save(deviceAccount);
				Console.WriteLine("SAVED DEVICE IDENTIFIER: " + deviceIdentifier);
			}
		}
	}
}