using System.Threading.Tasks;
using Azure.Messaging.ServiceBus;
using LoopDropSharp;
using MetaboyApiMessageProcessor.Models;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using Nethereum.Signer;
using Nethereum.Signer.EIP712;
using Nethereum.Util;
using PoseidonSharp;
using Type = LoopDropSharp.Type;
using System.Numerics;
using Dapper;
using System.Data;
using MetaboyApi.Models;
using System.Globalization;
using System.Data.SqlClient;

public class Program
{
    //connection string
    static string AzureServiceBusConnectionString = "";

    // name of your Service Bus queue
    static string queueName = "main";

    // the client that owns the connection and can be used to create senders and receivers
    static ServiceBusClient client;

    // the processor that reads and processes messages from the queue
    static ServiceBusProcessor processor;

    static ILoopringService loopringService;

    static Settings settings;

    static string AzureSqlConnectionString = "";

    static string loopringApiKey = "";
    static string loopringPrivateKey = "";
    static string MMorGMEPrivateKey = "";

    // Following are now stored in Azure KeyVault:
    // AzureServiceBusConnectionString
    // AzureSqlConnectionString
    // LoopringApiKey
    // LoopringPrivateKey
    // MMorGMEPrivateKey

    static async Task Main(string[] args)
    {
        // load services
        loopringService  = new LoopringService();

        //Settings loaded from the appsettings.json fileq
        IConfiguration config = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json")
            .AddEnvironmentVariables()
            .Build();
        settings = config.GetRequiredSection("Settings").Get<Settings>();

        string settingsSource = "";
        // Load values from Env supplied by Azure Key Vault
        if (settings.UseAzureKeyVault == true)
        {
            AzureServiceBusConnectionString = config.GetValue<string>("AzureServiceBusConnectionString");
            AzureSqlConnectionString = config.GetValue<string>("AzureSqlConnectionString");
            loopringApiKey = config.GetValue<string>("LoopringApiKey");
            loopringPrivateKey = config.GetValue<string>("LoopringPrivateKey");
            MMorGMEPrivateKey = config.GetValue<string>("MMorGMEPrivateKey");
            settingsSource = "Azure Key Vault via App Configuration Environment Settings";
        }
        // Use values from appsettings.json
        else
        {
            AzureServiceBusConnectionString = settings.AzureServiceBusConnectionString;
            AzureSqlConnectionString = settings.AzureSqlConnectionString;
            loopringApiKey = settings.LoopringApiKey;
            loopringPrivateKey = settings.LoopringPrivateKey;
            MMorGMEPrivateKey = settings.MMorGMEPrivateKey;
            settingsSource = "appsettings.json";
        }

        Console.WriteLine($"[ SETTINGS LOADED ]  :  {settingsSource}");

        var clientOptions = new ServiceBusClientOptions() { TransportType = ServiceBusTransportType.AmqpWebSockets };
        client = new ServiceBusClient(AzureServiceBusConnectionString, clientOptions);

        // create a processor that we can use to process the messages
        processor = client.CreateProcessor(queueName, new ServiceBusProcessorOptions() { MaxConcurrentCalls = 1, PrefetchCount = 1, AutoCompleteMessages = false });

        try
        {
            // add handler to process messages
            processor.ProcessMessageAsync += MessageHandler;

            // add handler to process any errors
            processor.ProcessErrorAsync += ErrorHandler;

            // start processing 
            await processor.StartProcessingAsync();
            Console.WriteLine("Waiting for messages...");

            while (true)
            {

            }
        }
        finally
        {
            // Calling DisposeAsync on client types is required to ensure that network
            // resources and other unmanaged objects are properly cleaned up.
            await processor.DisposeAsync();
            await client.DisposeAsync();
        }
    }

    // handle received messages
    static async Task MessageHandler(ProcessMessageEventArgs args)
    {
        string body = args.Message.Body.ToString();
        Console.WriteLine($"Received: {body}");
        NftReciever nftReciever = JsonConvert.DeserializeObject<NftReciever>(body);
        int? validStatus = null;
        string nftAmount = "";
        try
        {
            using (SqlConnection db = new System.Data.SqlClient.SqlConnection(AzureSqlConnectionString))
            {
                await db.OpenAsync();
                // Check if Nft is in Claimable
                var claimedListParameters = new { NftData = nftReciever.NftData };
                var claimedListSql = "SELECT * FROM Claimable WHERE nftdata = @NftData";
                var claimedListResult = await db.QueryAsync<Claimed>(claimedListSql, claimedListParameters);
                if (claimedListResult.Count() > 0)
                {
                    // Check if Nft is in Allowlist and obtain Amount
                    var allowListParameters = new { Address = nftReciever.Address, NftData = nftReciever.NftData };
                    var allowListSql = "SELECT * FROM Allowlist WHERE NftData = @NftData AND Address = @Address";
                    var allowListResult = await db.QueryAsync<AllowList>(allowListSql, allowListParameters);
                    if (allowListResult.Count() == 1)
                    {
                        nftAmount = allowListResult.First().Amount;
                        validStatus = 2; //valid continue
                        Console.WriteLine($"[INFO] Submitting Valid Claim for Transfer. Address: {nftReciever.Address} Nft : {nftReciever.NftData} Amount: {nftAmount}");
                    }
                    else
                    {
                        validStatus = 1; //not valid, don't continue
                    }
                }
                else
                {
                    validStatus = 0; //not valid, don't continue
                }
                await db.CloseAsync();
            }
        }
        catch (Exception ex)
        {
            validStatus = 0;
            Console.WriteLine(ex.Message);
        }

        if(validStatus == 2)
        {
            var fromAddress = settings.LoopringAddress; //your loopring address
            var fromAccountId = settings.LoopringAccountId; //your loopring account id
            var validUntil = settings.ValidUntil; //the examples seem to use this number
            var maxFeeTokenId = settings.MaxFeeTokenId; //0 should be for ETH, 1 is for LRC
            var exchange = settings.Exchange; //loopring exchange address, shouldn't need to change this,
            int toAccountId = 0; //leave this as 0 DO NOT CHANGE
            int nftTokenId;
            NftBalance userNftToken = new NftBalance();
            string nftData = nftReciever.NftData;
            string transferMemo = settings.TransferMemo;
            try
            {
                userNftToken = await loopringService.GetTokenIdWithCheck(loopringApiKey, settings.LoopringAccountId, nftData);

                nftTokenId = userNftToken.data[0].tokenId;
                var toAddress = nftReciever.Address;

                //Storage id
                var storageId = await loopringService.GetNextStorageId(loopringApiKey, fromAccountId, nftTokenId);

                //Getting the offchain fee
                var offChainFee = await loopringService.GetOffChainFee(loopringApiKey, fromAccountId, 11, "0");

                //Calculate eddsa signautre
                BigInteger[] poseidonInputs =
        {
                                    Utils.ParseHexUnsigned(exchange),
                                    (BigInteger) fromAccountId,
                                    (BigInteger) toAccountId,
                                    (BigInteger) nftTokenId,
                                    BigInteger.Parse(nftAmount),
                                    (BigInteger) maxFeeTokenId,
                                    BigInteger.Parse(offChainFee.fees[maxFeeTokenId].fee),
                                    Utils.ParseHexUnsigned(toAddress),
                                    (BigInteger) 0,
                                    (BigInteger) 0,
                                    (BigInteger) validUntil,
                                    (BigInteger) storageId.offchainId
                    };
                Poseidon poseidon = new Poseidon(13, 6, 53, "poseidon", 5, _securityTarget: 128);
                BigInteger poseidonHash = poseidon.CalculatePoseidonHash(poseidonInputs);
                Eddsa eddsa = new Eddsa(poseidonHash, loopringPrivateKey);
                string eddsaSignature = eddsa.Sign();

                //Calculate ecdsa
                string primaryTypeName = "Transfer";
                TypedData eip712TypedData = new TypedData();
                eip712TypedData.Domain = new Domain()
                {
                    Name = "Loopring Protocol",
                    Version = "3.6.0",
                    ChainId = 1,
                    VerifyingContract = "0x0BABA1Ad5bE3a5C0a66E7ac838a129Bf948f1eA4",
                };
                eip712TypedData.PrimaryType = primaryTypeName;
                eip712TypedData.Types = new Dictionary<string, MemberDescription[]>()
                {
                    ["EIP712Domain"] = new[]
                        {
                                            new MemberDescription {Name = "name", Type = "string"},
                                            new MemberDescription {Name = "version", Type = "string"},
                                            new MemberDescription {Name = "chainId", Type = "uint256"},
                                            new MemberDescription {Name = "verifyingContract", Type = "address"},
                                        },
                    [primaryTypeName] = new[]
                        {
                                            new MemberDescription {Name = "from", Type = "address"},            // payerAddr
                                            new MemberDescription {Name = "to", Type = "address"},              // toAddr
                                            new MemberDescription {Name = "tokenID", Type = "uint16"},          // token.tokenId 
                                            new MemberDescription {Name = "amount", Type = "uint96"},           // token.volume 
                                            new MemberDescription {Name = "feeTokenID", Type = "uint16"},       // maxFee.tokenId
                                            new MemberDescription {Name = "maxFee", Type = "uint96"},           // maxFee.volume
                                            new MemberDescription {Name = "validUntil", Type = "uint32"},       // validUntill
                                            new MemberDescription {Name = "storageID", Type = "uint32"}         // storageId
                                        },

                };
                eip712TypedData.Message = new[]
                {
                                    new MemberValue {TypeName = "address", Value = fromAddress},
                                    new MemberValue {TypeName = "address", Value = toAddress},
                                    new MemberValue {TypeName = "uint16", Value = nftTokenId},
                                    new MemberValue {TypeName = "uint96", Value = BigInteger.Parse(nftAmount)},
                                    new MemberValue {TypeName = "uint16", Value = maxFeeTokenId},
                                    new MemberValue {TypeName = "uint96", Value = BigInteger.Parse(offChainFee.fees[maxFeeTokenId].fee)},
                                    new MemberValue {TypeName = "uint32", Value = validUntil},
                                    new MemberValue {TypeName = "uint32", Value = storageId.offchainId},
                                };

                TransferTypedData typedData = new TransferTypedData()
                {
                    domain = new TransferTypedData.Domain()
                    {
                        name = "Loopring Protocol",
                        version = "3.6.0",
                        chainId = 1,
                        verifyingContract = "0x0BABA1Ad5bE3a5C0a66E7ac838a129Bf948f1eA4",
                    },
                    message = new TransferTypedData.Message()
                    {
                        from = fromAddress,
                        to = toAddress,
                        tokenID = nftTokenId,
                        amount = nftAmount,
                        feeTokenID = maxFeeTokenId,
                        maxFee = offChainFee.fees[maxFeeTokenId].fee,
                        validUntil = (int)validUntil,
                        storageID = storageId.offchainId
                    },
                    primaryType = primaryTypeName,
                    types = new TransferTypedData.Types()
                    {
                        EIP712Domain = new List<Type>()
                                        {
                                            new Type(){ name = "name", type = "string"},
                                            new Type(){ name="version", type = "string"},
                                            new Type(){ name="chainId", type = "uint256"},
                                            new Type(){ name="verifyingContract", type = "address"},
                                        },
                        Transfer = new List<Type>()
                                        {
                                            new Type(){ name = "from", type = "address"},
                                            new Type(){ name = "to", type = "address"},
                                            new Type(){ name = "tokenID", type = "uint16"},
                                            new Type(){ name = "amount", type = "uint96"},
                                            new Type(){ name = "feeTokenID", type = "uint16"},
                                            new Type(){ name = "maxFee", type = "uint96"},
                                            new Type(){ name = "validUntil", type = "uint32"},
                                            new Type(){ name = "storageID", type = "uint32"},
                                        }
                    }
                };

                Eip712TypedDataSigner signer = new Eip712TypedDataSigner();
                var ethECKey = new Nethereum.Signer.EthECKey(MMorGMEPrivateKey.Replace("0x", ""));
                var encodedTypedData = signer.EncodeTypedData(eip712TypedData);
                var ECDRSASignature = ethECKey.SignAndCalculateV(Sha3Keccack.Current.CalculateHash(encodedTypedData));
                var serializedECDRSASignature = EthECDSASignature.CreateStringSignature(ECDRSASignature);
                var ecdsaSignature = serializedECDRSASignature + "0" + (int)2;
                
                //Submit nft transfer
                var nftTransferResponse = await loopringService.SubmitNftTransfer(
                    apiKey: loopringApiKey,
                    exchange: exchange,
                    fromAccountId: fromAccountId,
                    fromAddress: fromAddress,
                    toAccountId: toAccountId,
                    toAddress: toAddress,
                    nftTokenId: nftTokenId,
                    nftAmount: nftAmount,
                    maxFeeTokenId: maxFeeTokenId,
                    maxFeeAmount: offChainFee.fees[maxFeeTokenId].fee,
                    storageId.offchainId,
                    validUntil: validUntil,
                    eddsaSignature: eddsaSignature,
                    ecdsaSignature: ecdsaSignature,
                    nftData: nftData,
                    transferMemo: transferMemo
                    );
                Console.WriteLine(nftTransferResponse);

                if(nftTransferResponse.Contains("process") || nftTransferResponse.Contains("received"))
                {
                    try
                    {
                        using (SqlConnection db = new System.Data.SqlClient.SqlConnection(AzureSqlConnectionString))
                        {
                            await db.OpenAsync();
                            var insertParameters = new
                            {
                                NftData = nftReciever.NftData,
                                Address = nftReciever.Address,
                                ClaimedDate = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss.fffffff",
                                       CultureInfo.InvariantCulture),
                                Amount = nftAmount
                            };
                            // Insert record into Completed Claims
                            var insertResult = await db.ExecuteAsync("INSERT INTO Claimed (Address,NftData,ClaimedDate,Amount) VALUES (@Address, @NftData, @ClaimedDate, @Amount)", insertParameters);
                            

                            var deleteParameters = new
                            {
                                NftData = nftReciever.NftData,
                                Address = nftReciever.Address

                            };
                            // Delete record from Available Claims
                            var deleteResult = await db.ExecuteAsync("DELETE FROM Allowlist WHERE Address = @Address AND NftData = @NftData", deleteParameters);
                            await db.CloseAsync();
                            Console.WriteLine($"Database Updated, Transferring to Address: {nftReciever.Address}  {nftAmount} of Nft: {nftReciever.NftData}");
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex.Message);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
            finally
            {
                try
                {
                    await args.CompleteMessageAsync(args.Message);
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                }

            }
        }
        else if(validStatus == 1)
        {
            try
            {
                Console.WriteLine($"This address: {nftReciever.Address}, is not in the allow list for nft: {nftReciever.NftData}");
                await args.CompleteMessageAsync(args.Message);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }
        else if(validStatus == 0)
        {
            try
            {
                Console.WriteLine($"This address: {nftReciever.Address}, has already claimed nft: {nftReciever.NftData}");
                await args.CompleteMessageAsync(args.Message);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }
        else
        {
            try
            {
                Console.WriteLine($"Something went wrong with address: {nftReciever.Address}, and nft: {nftReciever.NftData}");
                await args.CompleteMessageAsync(args.Message);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }
    }

    // handle any errors when receiving messages
    static Task ErrorHandler(ProcessErrorEventArgs args)
    {
        Console.WriteLine(args.Exception.ToString());
        return Task.CompletedTask;
    }

}
