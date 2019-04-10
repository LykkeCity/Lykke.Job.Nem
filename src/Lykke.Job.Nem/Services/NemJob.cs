using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Common;
using Common.Log;
using io.nem1.sdk.Infrastructure.HttpRepositories;
using io.nem1.sdk.Model.Accounts;
using io.nem1.sdk.Model.Transactions;
using io.nem1.sdk.Model.Transactions.Messages;
using Lykke.Common.Log;
using Lykke.Service.BlockchainApi.Sdk;

namespace Lykke.Job.Nem.Services
{
    public class NemJob : IBlockchainJob<string>
    {
        readonly Address _hotWallet;
        readonly string _nemUrl;
        readonly static DateTime _nemesis = new DateTime(2015, 03, 29, 0, 6, 25, 0, DateTimeKind.Utc);
        readonly int _requiredConfirmations;
        readonly ILog _log;

        public NemJob(string nemUrl, string hotWalletAddress, int requiredConfirmations, ILogFactory logFactory)
        {
            _hotWallet = Address.CreateFromEncoded(hotWalletAddress);
            _nemUrl = nemUrl;
            _requiredConfirmations = requiredConfirmations;
            _log = logFactory.CreateLog(this);
        }

        public async Task<(BlockchainAction[] actions, string state)> TraceDepositsAsync(string state, Func<string, Task<IAsset>> getAsset)
        {
            var lastConfirmedBlockNumber = await new BlockchainHttp(_nemUrl).GetBlockchainHeight() - (ulong)_requiredConfirmations;
            var actions = new List<BlockchainAction>();
            var accountHttp = new AccountHttp(_nemUrl);
            var txs = (await accountHttp.IncomingTransactions(_hotWallet))
                .Where(tx => tx.TransactionInfo.Height <= lastConfirmedBlockNumber)
                .OrderByDescending(tx => tx.TransactionInfo.Id)
                .ToList();
            var lastTransactionHash = txs.FirstOrDefault()?.TransactionInfo.Hash;
            var processedCount = 0;

            while (txs.Any())
            {
                _log.Info($"Retrieved {txs.Count} transactions");

                foreach (var tx in txs)
                {
                    if (tx.TransactionInfo.Hash == state)
                    {
                        _log.Info(processedCount > 0 
                            ? $"Processed {processedCount} transactions" 
                            : $"No new data since {lastTransactionHash}");

                        return (actions.ToArray(), lastTransactionHash);
                    }

                    if (tx.TransactionType != TransactionTypes.Types.Transfer)
                    {
                        _log.Warning($"Not a transfer, skipped", context: tx.TransactionInfo);
                        continue;
                    }

                    var transfer = tx as TransferTransaction ?? 
                        throw new InvalidOperationException($"Wrongly parsed transaction {tx.TransactionInfo.Hash}");

                    _log.Info($"New transfer detected", tx.TransactionInfo);

                    var blockNumber = (long)transfer.TransactionInfo.Height;
                    var blockTime = _nemesis.AddSeconds(transfer.TransactionInfo.TimeStamp);
                    var memo = (transfer.Message as PlainMessage)?.GetStringPayload().TrimAllSpacesAroundNullSafe();
                    var to = string.IsNullOrEmpty(memo)
                        ? transfer.Address.Plain
                        : transfer.Address.Plain + "$" + memo;

                    foreach (var mos in transfer.Mosaics)
                    {
                        var assetId = $"{mos.NamespaceName}:{mos.MosaicName}";
                        var asset = await getAsset(assetId);
                        if (asset == null)
                        {
                            _log.Info($"Unknown asset", new { assetId, mos.Amount });
                            continue;
                        }

                        var actionId = $"{asset.AssetId}:{mos.Amount}".CalculateHexHash32();
                        var amount = asset.FromBaseUnit((long)mos.Amount);

                        actions.Add(new BlockchainAction(actionId, blockNumber, blockTime, transfer.TransactionInfo.Hash, transfer.Signer.Address.Plain, asset.AssetId, (-1) * amount));
                        actions.Add(new BlockchainAction(actionId, blockNumber, blockTime, transfer.TransactionInfo.Hash, to, asset.AssetId, amount));
                    }

                    processedCount++;
                }

                txs = await accountHttp.IncomingTransactions(_hotWallet, new TransactionQueryParams(txs.Last().TransactionInfo.Hash));
            }

            _log.Info(processedCount > 0
                ? $"Processed {processedCount} transactions"
                : $"No new data since {lastTransactionHash}");

            return (actions.ToArray(), lastTransactionHash);
        }
    }
}