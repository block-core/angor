using UniSatWallet.Exceptions;
using Microsoft.JSInterop;
using System;
using System.Numerics;
using System.Reflection.Emit;
using System.Text.Json;
using System.Threading.Tasks;

namespace UniSatWallet
{
	// This class provides JavaScript functionality for UniSatWallet wrapped
	// in a .NET class for easy consumption. The associated JavaScript module is
	// loaded on demand when first needed.
	//
	// This class can be registered as scoped DI service and then injected into Blazor
	// components for use.

	public class UniSatWalletConnector : IAsyncDisposable, IUniSatWalletConnector
	{
		private readonly Lazy<Task<IJSObjectReference>> moduleTask;


		public UniSatWalletConnector(IJSRuntime jsRuntime)
		{
			moduleTask = new(() => LoadScripts(jsRuntime).AsTask());
		}

		public ValueTask<IJSObjectReference> LoadScripts(IJSRuntime jsRuntime)
		{
			return jsRuntime.InvokeAsync<IJSObjectReference>("import", "./_content/UniSatWallet/UniSatWallet.js");
		}


		public async ValueTask<bool> HasUniSatWallet()
		{
			var module = await moduleTask.Value;
			try
			{
				return await module.InvokeAsync<bool>("hasUniSatWallet");
			}
			catch (Exception ex)
			{
				HandleExceptions(ex);
				throw;
			}
		}

		public async ValueTask<string> ConnectUniSatWallet()
		{
			var module = await moduleTask.Value;
			try
			{
				var result = await module.InvokeAsync<string>("connectUniSatWallet");
				return result;
			}
			catch (Exception ex)
			{
				HandleExceptions(ex);
				throw;
			}
		}

		public async ValueTask<string> GetUniSatAccounts()
		{
			var module = await moduleTask.Value;
			try
			{
				var result = await module.InvokeAsync<string>("getUniSatAccounts");
				return result;
			}
			catch (Exception ex)
			{
				HandleExceptions(ex);
				throw;
			}
		}

		public async ValueTask<string> GetUniSatNetwork()
		{
			var module = await moduleTask.Value;
			try
			{
				var result = await module.InvokeAsync<string>("getUniSatNetwork");
				return result;
			}
			catch (Exception ex)
			{
				HandleExceptions(ex);
				throw;
			}
		}

		public async ValueTask<string> SwitchUniSatNetwork(string network)
		{
			var module = await moduleTask.Value;
			try
			{
				var result = await module.InvokeAsync<string>("switchUniSatNetwork", network);
				return result;
			}
			catch (Exception ex)
			{
				HandleExceptions(ex);
				throw;
			}
		}

		public async ValueTask<string> GetUniSatPublicKey()
		{
			var module = await moduleTask.Value;
			try
			{
				var result = await module.InvokeAsync<string>("getUniSatPublicKey");
				return result;
			}
			catch (Exception ex)
			{
				HandleExceptions(ex);
				throw;
			}
		}

		public async ValueTask<string> GetUniSatBalance()
		{
			var module = await moduleTask.Value;
			try
			{
				var result = await module.InvokeAsync<string>("getUniSatBalance");
				return result;
			}
			catch (Exception ex)
			{
				HandleExceptions(ex);
				throw;
			}
		}

		public async ValueTask<string> GetUniSatInscriptions(int cursor, int size)
		{
			var module = await moduleTask.Value;
			try
			{
				var result = await module.InvokeAsync<string>("getUniSatInscriptions", cursor, size);
				return result;
			}
			catch (Exception ex)
			{
				HandleExceptions(ex);
				throw;
			}
		}

		public async ValueTask<string> SendBitcoinUniSat(string toAddress, int satoshis, object options)
		{
			var module = await moduleTask.Value;
			try
			{
				var result = await module.InvokeAsync<string>("sendBitcoinUniSat", toAddress, satoshis, options);
				return result;
			}
			catch (Exception ex)
			{
				HandleExceptions(ex);
				throw;
			}
		}

		public async ValueTask<string> SendInscriptionUniSat(string address, string inscriptionId, object options)
		{
			var module = await moduleTask.Value;
			try
			{
				var result = await module.InvokeAsync<string>("sendInscriptionUniSat", address, inscriptionId, options);
				return result;
			}
			catch (Exception ex)
			{
				HandleExceptions(ex);
				throw;
			}
		}

		public async ValueTask<string> SignMessageUniSat(string msg, string type)
		{
			var module = await moduleTask.Value;
			try
			{
				var result = await module.InvokeAsync<string>("signMessageUniSat", msg, type);
				return result;
			}
			catch (Exception ex)
			{
				HandleExceptions(ex);
				throw;
			}
		}

		public async ValueTask<string> PushTransactionUniSat(object options)
		{
			var module = await moduleTask.Value;
			try
			{
				var result = await module.InvokeAsync<string>("pushTransactionUniSat", options);
				return result;
			}
			catch (Exception ex)
			{
				HandleExceptions(ex);
				throw;
			}
		}

		public async ValueTask<string> SignPsbtUniSat(string psbtHex, object options)
		{
			var module = await moduleTask.Value;
			try
			{
				var result = await module.InvokeAsync<string>("signPsbtUniSat", psbtHex, options);
				return result;
			}
			catch (Exception ex)
			{
				HandleExceptions(ex);
				throw;
			}
		}

		public async ValueTask<string> SignPsbtsUniSat(string[] psbtHexs, object options)
		{
			var module = await moduleTask.Value;
			try
			{
				var result = await module.InvokeAsync<string>("signPsbtsUniSat", psbtHexs, options);
				return result;
			}
			catch (Exception ex)
			{
				HandleExceptions(ex);
				throw;
			}
		}

		public async ValueTask<string> PushPsbtUniSat(string psbtHex)
		{
			var module = await moduleTask.Value;
			try
			{
				var result = await module.InvokeAsync<string>("pushPsbtUniSat", psbtHex);
				return result;
			}
			catch (Exception ex)
			{
				HandleExceptions(ex);
				throw;
			}
		}

		public async ValueTask DisposeAsync()
		{
			if (moduleTask.IsValueCreated)
			{
				var module = await moduleTask.Value;
				await module.DisposeAsync();
			}
		}

		private void HandleExceptions(Exception ex)
		{
			switch (ex.Message)
			{
				case "NoUniSatWallet":
					throw new NoUniSatWalletException();
				case "UserDenied":
					throw new UserDeniedException();
			}
		}

	}
}
