using System;
using System.Threading.Tasks;
using AElf.Common;
using AElf.CrossChain;
using AElf.Kernel.Blockchain.Application;
using AElf.Kernel.Blockchain.Domain;
using AElf.Kernel.Blockchain.Events;
using AElf.Kernel.SmartContractExecution.Domain;
using AElf.Kernel.Types;
using AElf.Types.CSharp;
using Google.Protobuf;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Volo.Abp.DependencyInjection;
using Volo.Abp.EventBus;
using Volo.Abp.EventBus.Local;

namespace AElf.Kernel
{
    // ReSharper disable InconsistentNaming
    public class LibBestChainFoundEventHandler : ILocalEventHandler<BestChainFoundEventData>, ITransientDependency
    {
        private readonly IBlockchainService _blockchainService;
        private readonly IBlockManager _blockManager;
        private readonly ITransactionResultManager _transactionResultManager;
        private readonly IChainManager _chainManager;
        private readonly IBlockchainStateManager _blockchainStateManager;

        public ILogger<LibBestChainFoundEventHandler> Logger { get; set; }

        public ILocalEventBus LocalEventBus { get; set; }

        public LibBestChainFoundEventHandler(IBlockchainService blockchainService, IBlockManager blockManager,
            ITransactionResultManager transactionResultManager, IChainManager chainManager, IBlockchainStateManager blockchainStateManager)
        {
            _blockchainService = blockchainService;
            _blockManager = blockManager;
            _transactionResultManager = transactionResultManager;
            _chainManager = chainManager;
            _blockchainStateManager = blockchainStateManager;
            LocalEventBus = NullLocalEventBus.Instance;

            Logger = NullLogger<LibBestChainFoundEventHandler>.Instance;
        }


        public async Task HandleEventAsync(BestChainFoundEventData eventData)
        {
            if (eventData.ExecutedBlocks == null)
            {
                return;
            }

            foreach (var executedBlock in eventData.ExecutedBlocks)
            {
                var block = await _blockManager.GetBlockAsync(executedBlock);

                foreach (var transactionHash in block.Body.Transactions)
                {
                    var result = await _transactionResultManager.GetTransactionResultAsync(transactionHash);
                    foreach (var contractEvent in result.Logs)
                    {
                        if (contractEvent.Address ==
                            ContractHelpers.GetConsensusContractAddress(block.Header.ChainId) &&
                            contractEvent.Topics.Contains(
                                ByteString.CopyFrom(Hash.FromString("LIBFound").DumpByteArray())))
                        {
                            var indexingEventData = ExtractLibFoundData(contractEvent);
                            var offset = (ulong) indexingEventData[0];
                            var libHeight = eventData.BlockHeight - offset;

                            Logger.LogInformation($"Lib Height: {libHeight}");

                            var chain = await _blockchainService.GetChainAsync(eventData.ChainId);
                            var libHash = await _blockchainService.GetBlockHashByHeightAsync(chain, libHeight);
                            await _chainManager.SetIrreversibleBlockAsync(chain, libHash);

                            Logger.LogInformation($"Lib Hash: {libHash}");

                            //await _blockchainStateManager.MergeBlockStateAsync(eventData.ChainId, libHash);
                        }

                        //await LocalEventBus.PublishAsync(contractEvent);
                    }
                }
            }
        }

        private object[] ExtractLibFoundData(LogEvent logEvent)
        {
            return ParamsPacker.Unpack(logEvent.Data.ToByteArray(), new[] {typeof(ulong)});
        }
    }
}