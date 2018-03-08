﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Text;
using Newtonsoft.Json;


#if !__UNIFIED__
using MonoTouch.Security;
using MonoTouch.Foundation;
using MonoTouch.ObjCRuntime;
using MonoTouch.CoreFoundation;
using Monotouch.UIKit;
#else
using Security;
using Foundation;
using ObjCRuntime;
using CoreFoundation;
using UIKit;
#endif


namespace Xamarin.SecureAccountStore.CustomCode
{
	internal partial class KeyChainAccountStore
	{
		static Lazy<System.Reflection.MethodInfo> SecRecord_queryDictGetter = new Lazy<System.Reflection.MethodInfo>(() =>
		{
			return typeof(SecRecord).GetProperty("queryDict", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetMethod;
		});

		static Lazy<IntPtr> Security_ReturnData = new Lazy<IntPtr>
			(
				() =>
				{
					return (IntPtr)typeof(SecRecord).Assembly.GetTypes()
								.First(t => t.Name == "SecItem"
							   && t.Namespace == (typeof(SecRecord)).Namespace /*"[MonoTouch.]Security"*/
								).GetProperty("ReturnData").GetMethod.Invoke(null, new Object[] { });
				}
			);

		static Lazy<INativeObject> CFBoolean_True = new Lazy<INativeObject>(() =>
		{
			return typeof(SecRecord).Assembly.GetTypes()
						.First(t => t.Name == "CFBoolean"
					   && t.Namespace == (typeof(CFObject)).Namespace /* "[MonoTouch.]CoreFoundation" */
						).GetField("True").GetValue(null) as INativeObject;
		});

		public Task<List<Account>> FindAccountsForServiceAsync(string serviceId)
		{
			SecRecord[] records = null;

			try
			{
				var query = new SecRecord(SecKind.GenericPassword);
				query.Service = serviceId;

				// Workaround for https://bugzilla.xamarin.com/show_bug.cgi?id=29977
				var queryDict = SecRecord_queryDictGetter.Value.Invoke(query, new object[] { }) as NSMutableDictionary;
				queryDict.LowlevelSetObject(CFBoolean_True.Value.Handle, Security_ReturnData.Value);

				SecStatusCode result;
				records = SecKeyChain.QueryAsRecord(query, 1000, out result);
			}
			catch (System.Exception exc)
			{
				string msg = String.Format("Search/Find FindAccountsForServiceAsync {0}", exc.Message);
				throw new InvalidOperationException(msg, exc);
			}

			IEnumerable<Account> accounts_found = null;
			IEnumerable<Account> ienumerable_accounts = null;
			if (records != null)
			{
				/*
                moljac note:
                ienumerable_accounts    
                    {
                        System.Linq.Enumerable.WhereSelectArrayIterator
                                                <
                                                    MonoTouch.Security.SecRecord,
                                                    Xamarin.Auth.Account
                                                >
                    }    
                    {
                        System.Linq.Enumerable.WhereSelectArrayIterator
                                                <
                                                    Security.SecRecord,
                                                    Xamarin.Auth.Account
                                                >
                    }    
                */
				ienumerable_accounts = records.Select(GetAccountFromRecord);

				/*
                    must check for empty IEnumerable
                    IEnumerable ToList()
                    Value cannot be null.
                    Parameter name: data
                */
				try
				{
					/*
                        Classic
                        accessing throws
                        > ienumerable_accounts.Count()
                        System.ArgumentNullException: Value cannot be null.
                        Parameter name: data
                        > ienumerable_accounts.LongCount()
                        System.ArgumentNullException: Value cannot be null.
                        Parameter name: data 
                    */
					if (ienumerable_accounts.Count() > 0 && ienumerable_accounts.LongCount() > 0)
					{
						/*
                            Unified enters
                            method call ToList() throws
                            > ienumerable_accounts.Count()
                            System.ArgumentNullException: Value cannot be null.
                            Parameter name: data
                            > ienumerable_accounts.LongCount()
                            System.ArgumentNullException: Value cannot be null.
                            Parameter name: data
                            > ienumerable_accounts.ToList()
                            System.ArgumentNullException: Value cannot be null.
                            Parameter name: data
                        */
						accounts_found = ienumerable_accounts.ToList();
					}
					else
					{
						accounts_found = new List<Account>();
					}
				}
				catch (System.Exception exc)
				{
					string msg = exc.Message;
					Debug.WriteLine("IEnumerable access excption = " + msg);
					// throw new Xamarin.Auth.AccountStoreException("IEnumerable access excption = " + msg);
				}
			}
			else
			{
				accounts_found = new List<Account>();
			}

			List<Account> retval = new List<Account>(accounts_found);

			return Task.FromResult(retval);
		}

		Account GetAccountFromRecord(SecRecord r)
		{
			Account a = null;

			try
			{
				NSData data_generic_unencrypted = r.Generic;
				NSData data_valuedata_encrypted = r.ValueData;

				NSData data = null;

				if (data_generic_unencrypted != null)
				{
					// old API - unencrypted/insecure/unsafe
					data = data_generic_unencrypted;
				}
				else if (data_valuedata_encrypted != null)
				{
					// new API - encrypted/secure/safe
					data = data_valuedata_encrypted;
				}

				NSString serializedData = NSString.FromData(data, NSStringEncoding.UTF8);
				a = JsonConvert.DeserializeObject<Account>(serializedData);
			}
			catch (System.Exception exc)
			{
				string msg = String.Format("GetAccountFromRecord error = {0}", exc.Message);
				Debug.WriteLine(msg);
				throw new InvalidOperationException(msg);
			}

			return a;
		}

		Account FindAccount(string username, string serviceId)
		{
			Account a = null;

			try
			{
				SecRecord query = new SecRecord(SecKind.GenericPassword);
				query.Service = serviceId;
				query.Account = username;

				SecStatusCode result;
				SecRecord record = SecKeyChain.QueryAsRecord(query, out result);

				a = record != null ? GetAccountFromRecord(record) : null;
			}
			catch (System.Exception exc)
			{
				string msg = String.Format("FindAccount error = {0}", exc.Message);
				Debug.WriteLine(msg);
				throw new InvalidOperationException(msg);
			}

			return a;
		}

		public Task SaveAsync(Account account, string serviceId)
		{
			try
			{
				SecStatusCode statusCode = SecStatusCode.Success;
				string serializedAccount = JsonConvert.SerializeObject(account);
				NSData data = NSData.FromString(serializedAccount, NSStringEncoding.UTF8);

				//
				// Remove any existing record
				//
				var existing = FindAccount(account.Username, serviceId);

				if (existing != null)
				{
					var query = new SecRecord(SecKind.GenericPassword);
					query.Service = serviceId;
					query.Account = account.Username;

					statusCode = SecKeyChain.Remove(query);
					if (statusCode != SecStatusCode.Success)
					{
						throw new InvalidOperationException("Could not remove account from KeyChain: " + statusCode);
					}
				}

				//
				// Add this record
				//
				SecRecord record = new SecRecord(SecKind.GenericPassword)
				{
					Service = serviceId,
					Account = account.Username,
					//------------------------------------------------------
					// mc++ mc#
					// Mark Taparauskas suggetsion:
					//      .Generic is not encrypted
#if TEST_MARK_T
                    Generic = data,
#else
					ValueData = data,
#endif
				};
				//------------------------------------------------------
				record.Accessible =
								//SecAccessible.WhenUnlocked
								// Pull Request - manually added/fixed
								//      Changed SecAccessible.WhenUnLocked to AfterFirstUnLocked #80
								//      https://github.com/xamarin/Xamarin.Auth/pull/80
								SecAccessible.AfterFirstUnlockThisDeviceOnly ////THIS IS THE FIX
																			 // ???
																			 // SecAccessible.AlwaysThisDeviceOnly
								;

				statusCode = SecKeyChain.Add(record);

				if (statusCode != SecStatusCode.Success)
				{
					StringBuilder sb = new StringBuilder("error = ");
					sb.AppendLine("Could not save account to KeyChain: " + statusCode);
					sb.AppendLine("Add Empty Entitlements.plist ");
					sb.AppendLine(" File /+ New file /+ iOS /+ Entitlements.plist");
					/*
                        Error: Could not save account to KeyChain -- iOS 10 #128
                        https://github.com/xamarin/Xamarin.Auth/issues/128 
                        https://bugzilla.xamarin.com/show_bug.cgi?id=43514
                        sb.AppendLine("");
                    */
					if ((int)statusCode == -34018)
					{
						// http://stackoverflow.com/questions/38456471/secitemadd-always-returns-error-34018-in-xcode-8-in-ios-10-simulator
						// NOTE: code was not copy/pasted! That was iOS sample

						sb.AppendLine("SecKeyChain.Add returned : " + statusCode);
						sb.AppendLine("1. Add Keychain Access Groups to the Entitlements file.");
						sb.AppendLine("2. Turn on the Keychain Sharing switch in the Capabilities section in the app.");
					}
					string msg = sb.ToString();

					throw new InvalidOperationException(msg);
				}
			}
			catch (System.Exception exc)
			{
				string msg = String.Format("SaveAsync error = {0}", exc.Message);
				Debug.WriteLine(msg);
				throw new InvalidOperationException(msg);
			}

			return Task.FromResult(true);
		}

		public Task DeleteAsync(Account account, string serviceId)
		{
			try
			{
				var query = new SecRecord(SecKind.GenericPassword);
				query.Service = serviceId;
				query.Account = account.Username;

				var statusCode = SecKeyChain.Remove(query);

				if (statusCode != SecStatusCode.Success)
				{
					throw new InvalidOperationException("Could not delete account from KeyChain: " + statusCode);
				}
			}
			catch (System.Exception exc)
			{
				string msg = String.Format("DeleteAsync error = {0}", exc.Message);
				Debug.WriteLine(msg);
				throw new InvalidOperationException(msg);
			}

			return Task.FromResult(true);
		}
	}
}