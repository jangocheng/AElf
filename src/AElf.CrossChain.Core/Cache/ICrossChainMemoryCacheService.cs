using System.Collections.Generic;
using AElf.Kernel;

namespace AElf.CrossChain.Cache
{
    public interface ICrossChainMemoryCacheService
    {
        void RegisterNewChainCache(int remoteChainId, long chainHeight = Constants.GenesisBlockHeight);
        int GetCachedChainCount();
        long GetNeededChainHeightForCache(int chainId);
        IEnumerable<int> GetCachedChainIds();
    }
}