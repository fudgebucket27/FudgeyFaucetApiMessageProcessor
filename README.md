# MetaboyApiMessageProcessor
.NET 6 project to process messages in the Metaboy Azure Service Bus message queue.

## NFT Claims
At the moment there is one queue where messages are added to for the NFT claims. The message processor will process the messages in the queue in a First In, First Out basis. It will use the details in the messages to transfer the NFTs from the [distribution wallet](https://lexplorer.io/account/191813) to the reciever. 

The reason why a queue is need is that the storage id needs to be calculated correctly in order of transfer for the Loopring API. This also allows for load leveling as the rate of transfers will be constant and we won't be rate limited by the Loopring API too. 

## Database setup
The database should have the following tables:

![image](https://user-images.githubusercontent.com/5258063/197443511-35e9812e-75df-4fff-9fef-fdd0062c88b2.png)


# Setup
You need to create an appsettings.json file within the root directory with the following properties, replacing with your own values

```json
{
  "Settings": {
    "LoopringApiKey": ", //Your loopring api key.  DO NOT SHARE THIS AT ALL.
    "LoopringPrivateKey": "", //Your loopring private key.  DO NOT SHARE THIS AT ALL.
    "LoopringAddress": "", //Your loopring address
    "LoopringAccountId": 191813, //Your loopring account id
    "ValidUntil": 1700000000, //How long this transfer should be valid for. Shouldn't have to change this value
    "MaxFeeTokenId": 1, //The token id for the fee. 0 for ETH, 1 for LRC
    "Exchange": "0x0BABA1Ad5bE3a5C0a66E7ac838a129Bf948f1eA4", //Loopring Exchange address,
    "MMorGMEPrivateKey": "", //Private key from metamask or gamestop wallet. DO NOT SHARE THIS AT ALL.
    "AzureServiceBusConnectionString": "", //Service Bus connection, DO NOT SHARE THIS AT ALL
    "AzureSqlConnectionString": "" //Sql Connection DO NOT SHARE THIS AT ALL
  }
}
```

Finally, setup an Azure Service Bus queue with the name "main".

Deploy this project as a Continuous WebJob on Azure.


# Credits
Portions of code used in [LoopDropSharp](https://github.com/cobmin/LoopDropSharp) by [Cobmin](https://twitter.com/cobmin) which is a fork based on code originally written by Fudgey.eth, were used to help bootstrap development.
