﻿using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Foundation;
using Newtonsoft.Json;
using Security;

namespace Xamarin.SecureAccountStore.CustomCode
{
	public class MyTinyiOSAccountManager : IAccountManager
	{
		public async Task Save(Account account)
		{
			if (string.IsNullOrWhiteSpace(account.ServiceId))
			{
				throw new Exception("serviceId must be set.");
			}

			var data = JsonConvert.SerializeObject(account.Properties);

			var secRecord = new SecRecord(SecKind.GenericPassword)
			{
				Account = account.Username,
				Service = account.ServiceId,
				ValueData = NSData.FromString(data, NSStringEncoding.UTF8),
				Accessible = SecAccessible.AfterFirstUnlockThisDeviceOnly
			};

			var old = await Find(account.ServiceId);

			if (old == null)
			{
				var statusCode = SecKeyChain.Add(secRecord);

				return;
			}

			SecKeyChain.Update(old, secRecord);

		}

		private async Task<SecRecord> Find(string serviceId)
		{
			var query = new SecRecord(SecKind.GenericPassword)
			{
				Service = serviceId
			};

			SecStatusCode status;

			var result = SecKeyChain.QueryAsRecord(query, out status);

			if (status == SecStatusCode.Success)
			{
				return result;
			}

			return null;
		}

		public async Task<bool> Exists(string serviceId)
		{
			var result = await Find(serviceId);

			return (result != null);
		}

		public async Task<Account> Get(string serviceId)
		{
			var result = await Find(serviceId);

			if (result == null)
			{
				throw new Exception(string.Format("Account with serviceId: {0}, does not exists", serviceId));
			}

			var account = new Account()
			{
				Username = result.Account,
				Properties = JsonConvert.DeserializeObject<Dictionary<string, string>>(result.ValueData.ToString())
			};

			return account;
		}

		public async Task Remove(string serviceId)
		{

			var query = new SecRecord(SecKind.GenericPassword)
			{
				Service = serviceId
			};

			var status = SecKeyChain.Remove(query);

			if (status != SecStatusCode.Success)
			{
				throw new Exception("Account cannot be removed. Status code: " + status);
			}
		}
	}
}
