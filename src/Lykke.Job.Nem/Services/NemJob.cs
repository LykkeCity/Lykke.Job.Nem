using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Common;
using io.nem1.sdk.Infrastructure.HttpRepositories;
using io.nem1.sdk.Model.Accounts;
using io.nem1.sdk.Model.Transactions;
using Lykke.Service.BlockchainApi.Sdk;

namespace Lykke.Job.Nem.Services
{
    public class NemJob : IBlockchainJob<string>
    {
        readonly Address _hotWallet;
        readonly string _nemUrl;
        readonly static DateTime _nemesis = new DateTime(2015, 03, 29, 0, 6, 25, 0).ToUniversalTime();

        public NemJob(string nemUrl, string hotWalletAddress)
        {
            _hotWallet = Address.CreateFromEncoded(hotWalletAddress);
            _nemUrl = nemUrl;
        }

        public async Task<(BlockchainAction[] actions, string state)> TraceDepositsAsync(string state, Func<string, Task<IAsset>> getAsset)
        {
            var actions = new List<BlockchainAction>();
            var accountHttp = new AccountHttp(_nemUrl);
            var txs = await accountHttp.IncomingTransactions(_hotWallet);
            var lastTransactionHash = txs.LastOrDefault()?.TransactionInfo.Hash;

            while (txs.Any())
            {
                foreach (var tx in txs.OrderByDescending(t => t.TransactionInfo.Id))
                {
                    if (tx.TransactionInfo.Hash == state)
                        return (actions.ToArray(), lastTransactionHash);

                    if (tx.TransactionType != TransactionTypes.Types.Transfer)
                        continue;

                    var transfer = tx as TransferTransaction ?? 
                        throw new InvalidOperationException($"Wrongly parsed transaction {tx.TransactionInfo.Hash}");

                    var blockNumber = (long)transfer.TransactionInfo.Height;
                    var blockTime = _nemesis.AddSeconds(transfer.TransactionInfo.TimeStamp);

                    foreach (var mos in transfer.Mosaics)
                    {
                        var asset = await getAsset($"{mos.NamespaceName}:{mos.MosaicName}");
                        if (asset == null)
                            continue;

                        var actionId = $"{asset.AssetId}:{mos.Amount}".CalculateHexHash32();
                        var amount = asset.FromBaseUnit((long)mos.Amount);

                        actions.Add(new BlockchainAction(actionId, blockNumber, blockTime, transfer.TransactionInfo.Hash, transfer.Signer.Address.Plain, asset.AssetId, (-1) * amount));
                        actions.Add(new BlockchainAction(actionId, blockNumber, blockTime, transfer.TransactionInfo.Hash, transfer.Address.Plain, asset.AssetId, amount));
                    }
                }

                txs = await accountHttp.IncomingTransactions(_hotWallet, new TransactionQueryParams(txs.First().TransactionInfo.Hash));
            }

            return (actions.ToArray(), lastTransactionHash);
        }
    }
}