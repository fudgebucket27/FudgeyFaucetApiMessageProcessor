using LoopDropSharp;
using Nethereum.Signer.EIP712;
using Nethereum.Util;
using PoseidonSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace LoopDropSharp
{
    public static class Utils
    {

        //public static void CheckForAppsettingsDotJson()
        //{
        //    string fileName = @".\..\..\..\appsettings.json";

        //    try
        //    {
        //        if (!File.Exists(fileName))
        //        {
        //            // Create a new file     
        //            using (StreamWriter sw = File.CreateText(fileName))
        //            {
        //                {
        //                    sw.Write("{");
        //                    sw.Write("  \"Settings\": {\r\n");
        //                    sw.Write("    \"LoopringApiKey\": \"ksdBlahblah\", //Your loopring api key.  DO NOT SHARE THIS AT ALL.\r\n");
        //                    sw.Write("    \"LoopringPrivateKey\": \"0xblahblah\", //Your loopring private key.  DO NOT SHARE THIS AT ALL.\r\n");
        //                    sw.Write("    \"MetamaskPrivateKey\": \"asadasdBLahBlah\", //Private key from metamask. DO NOT SHARE THIS AT ALL.\r\n");
        //                    sw.Write("    \"LoopringAddress\": \"0xblahabla\", //Your loopring address\r\n");
        //                    sw.Write("    \"LoopringAccountId\": 40940, //Your loopring account id\r\n");
        //                    sw.Write("    \"ValidUntil\": 1700000000, //How long this transfer should be valid for. Shouldn't have to change this value\r\n");
        //                    sw.Write("    \"MaxFeeTokenId\": 1, //The token id for the fee. 0 for ETH, 1 for LRC\r\n");
        //                    sw.Write("    \"Exchange\": \"0x0BABA1Ad5bE3a5C0a66E7ac838a129Bf948f1eA4\" //Loopring Exchange address\r\n");
        //                    sw.Write("  }\r\n");
        //                    sw.Write("}");
        //                }
        //            }
        //            Font.SetTextToRed("The Appsettings.json file is not setup. Please set it up before proceeding.");
        //            Font.SetTextToYellow("Watch this video for more information, https://www.youtube.com/watch?v=Bkl6BwfA6jE&t=18s.");
        //            Font.SetTextToYellow("The file's properties > Copy to Output Directory might need to be set to 'Copy Always'.");
        //            Font.SetTextToYellow("Application may need to be restarted after changes are made.");
        //            Font.SetTextToBlue("Are you ready?");
        //            var userResponseReadyToMoveOn = Utils.CheckYes();
        //        }
        //    }
        //    catch (Exception Ex)
        //    {
        //        Font.SetTextToWhite(Ex.ToString());
        //    }
        //}
        public static BigInteger ParseHexUnsigned(string toParse)
        {
            toParse = toParse.Replace("0x", "");
            var parsResult = BigInteger.Parse(toParse, System.Globalization.NumberStyles.HexNumber);
            if (parsResult < 0)
                parsResult = BigInteger.Parse("0" + toParse, System.Globalization.NumberStyles.HexNumber);
            return parsResult;
        }
    }
}
