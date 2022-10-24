# MetaboyApiMessageProcessor
.NET 6 project to process messages in the Metaboy Azure Service Bus message queue.

# NFT Claims
At the moment there is one queue where messages are added to for the NFT claims. The message processor will process the messages in the queue in a First In, First Out basis. It will use the details in the messages to transfer the NFTs from the [distribution wallet](https://lexplorer.io/account/191813) to the reciever. 

The reason why a queue is need is that the storage id needs to be calculated correctly in order of transfer for the Loopring API. This also allows for load leveling as the rate of transfers will be constant and we won't be rate limited by the Loopring API too.
