using Microsoft.JSInterop;
using System;
using System.Collections.Generic;
using System.Net;
using System.Numerics;
using System.Threading.Tasks;

namespace UniSatWallet
{
	public interface IUniSatWalletConnector
	{
		ValueTask<bool> HasUniSatWallet();
		ValueTask<string> ConnectUniSatWallet();
		ValueTask<string> GetUniSatAccounts();
		ValueTask<string> GetUniSatNetwork();
		ValueTask<string> SwitchUniSatNetwork(string network); //the network."livenet" and "testnet"
		ValueTask<string> GetUniSatPublicKey();
		ValueTask<string> GetUniSatBalance();
		ValueTask<string> GetUniSatInscriptions(int cursor, int size);
		ValueTask<string> SendBitcoinUniSat(string toAddress, int satoshis, object options);
		ValueTask<string> SendInscriptionUniSat(string address, string inscriptionId, object options);
		ValueTask<string> SignMessageUniSat(string msg, string type);
		ValueTask<string> PushTransactionUniSat(object options);
		ValueTask<string> SignPsbtUniSat(string psbtHex, object options);
		ValueTask<string> SignPsbtsUniSat(string[] psbtHexs, object options);
		ValueTask<string> PushPsbtUniSat(string psbtHex);
	}

}