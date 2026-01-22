using Angor.Sdk.Funding.Projects.Domain;
using Angor.Sdk.Funding.Shared;
using Angor.Shared.Models;
using Blockcore.NBitcoin;
using Blockcore.NBitcoin.DataEncoders;
using Stage = Angor.Shared.Models.Stage;

namespace Angor.Sdk.Tests.Shared;

/// <summary>
/// Builder for creating test data objects with sensible defaults.
/// Supports fluent API for customization.
/// </summary>
public static class TestDataBuilder
{
    #region ProjectInfo Builder
    
    public static ProjectInfoBuilder CreateProjectInfo() => new();
    
    public class ProjectInfoBuilder
    {
        private string _projectIdentifier = Encoders.Hex.EncodeData(RandomUtils.GetBytes(32));
        private string _founderKey = Encoders.Hex.EncodeData(RandomUtils.GetBytes(33));
        private string _founderRecoveryKey = Encoders.Hex.EncodeData(RandomUtils.GetBytes(33));
        private string _nostrPubKey = Encoders.Hex.EncodeData(RandomUtils.GetBytes(32));
        private long _targetAmount = Money.Coins(100).Satoshi;
        private DateTime _startDate = DateTime.UtcNow;
        private DateTime _expiryDate = DateTime.UtcNow.AddDays(30);
        private DateTime _penaltyEndDate = DateTime.UtcNow.AddDays(45);
        private int _stageCount = 3;
        private ProjectType _projectType = ProjectType.Invest;
        
        public ProjectInfoBuilder WithProjectId(string projectId)
        {
            _projectIdentifier = projectId;
            return this;
        }
        
        public ProjectInfoBuilder WithFounderKey(string founderKey)
        {
            _founderKey = founderKey;
            return this;
        }
        
        public ProjectInfoBuilder WithFounderRecoveryKey(string founderRecoveryKey)
        {
            _founderRecoveryKey = founderRecoveryKey;
            return this;
        }
        
        public ProjectInfoBuilder WithTargetAmount(long satoshis)
        {
            _targetAmount = satoshis;
            return this;
        }
        
        public ProjectInfoBuilder WithStages(int count)
        {
            _stageCount = count;
            return this;
        }
        
        public ProjectInfoBuilder WithStartDate(DateTime date)
        {
            _startDate = date;
            return this;
        }
        
        public ProjectInfoBuilder WithExpiryDate(DateTime date)
        {
            _expiryDate = date;
            return this;
        }
        
        public ProjectInfoBuilder WithProjectType(ProjectType type)
        {
            _projectType = type;
            return this;
        }
        
        public ProjectInfo Build()
        {
            var stages = new List<Stage>();
            var ratioPerStage = 100m / _stageCount;
            
            for (int i = 0; i < _stageCount; i++)
            {
                stages.Add(new Stage
                {
                    AmountToRelease = ratioPerStage,
                    ReleaseDate = _startDate.AddDays((i + 1) * 10)
                });
            }
            
            return new ProjectInfo
            {
                ProjectIdentifier = _projectIdentifier,
                FounderKey = _founderKey,
                FounderRecoveryKey = _founderRecoveryKey,
                NostrPubKey = _nostrPubKey,
                TargetAmount = _targetAmount,
                StartDate = _startDate,
                ExpiryDate = _expiryDate,
                PenaltyDays = 15,
                Stages = stages,
                ProjectType = _projectType
            };
        }
    }
    
    #endregion
    
    #region Project Builder
    
    public static ProjectBuilder CreateProject() => new();
    
    public class ProjectBuilder
    {
        private ProjectId _id = new(Encoders.Hex.EncodeData(RandomUtils.GetBytes(32)));
        private string _name = "Test Project";
        private string _shortDescription = "A test project for unit testing";
        private string _founderKey = Encoders.Hex.EncodeData(RandomUtils.GetBytes(33));
        private string _founderRecoveryKey = Encoders.Hex.EncodeData(RandomUtils.GetBytes(33));
        private string _nostrPubKey = Encoders.Hex.EncodeData(RandomUtils.GetBytes(32));
        private long _targetAmount = Money.Coins(100).Satoshi;
        private DateTime _startingDate = DateTime.UtcNow;
        private DateTime _expiryDate = DateTime.UtcNow.AddDays(30);
        private DateTime _endDate = DateTime.UtcNow.AddDays(30);
        private TimeSpan _penaltyDuration = TimeSpan.FromDays(15);
        private int _stageCount = 3;
        private ProjectType _projectType = ProjectType.Invest;
        private List<DynamicStagePattern>? _dynamicStagePatterns;
        
        public ProjectBuilder WithId(string projectId)
        {
            _id = new ProjectId(projectId);
            return this;
        }
        
        public ProjectBuilder WithName(string name)
        {
            _name = name;
            return this;
        }
        
        public ProjectBuilder WithFounderKey(string founderKey)
        {
            _founderKey = founderKey;
            return this;
        }
        
        public ProjectBuilder WithFounderRecoveryKey(string founderRecoveryKey)
        {
            _founderRecoveryKey = founderRecoveryKey;
            return this;
        }
        
        public ProjectBuilder WithTargetAmount(long satoshis)
        {
            _targetAmount = satoshis;
            return this;
        }
        
        public ProjectBuilder WithStages(int count)
        {
            _stageCount = count;
            return this;
        }
        
        public ProjectBuilder WithStartingDate(DateTime date)
        {
            _startingDate = date;
            return this;
        }
        
        public ProjectBuilder WithProjectType(ProjectType type)
        {
            _projectType = type;
            return this;
        }
        
        public ProjectBuilder WithDynamicStagePatterns(List<DynamicStagePattern> patterns)
        {
            _dynamicStagePatterns = patterns;
            return this;
        }
        
        public Project Build()
        {
            var stages = new List<Angor.Sdk.Funding.Projects.Domain.Stage>();
            var ratioPerStage = 1m / _stageCount;
            
            for (int i = 0; i < _stageCount; i++)
            {
                stages.Add(new Angor.Sdk.Funding.Projects.Domain.Stage
                {
                    Index = i,
                    RatioOfTotal = ratioPerStage,
                    ReleaseDate = _startingDate.AddDays((i + 1) * 10)
                });
            }
            
            return new Project
            {
                Id = _id,
                Name = _name,
                ShortDescription = _shortDescription,
                FounderKey = _founderKey,
                FounderRecoveryKey = _founderRecoveryKey,
                NostrPubKey = _nostrPubKey,
                TargetAmount = _targetAmount,
                StartingDate = _startingDate,
                ExpiryDate = _expiryDate,
                EndDate = _endDate,
                PenaltyDuration = _penaltyDuration,
                Stages = stages,
                ProjectType = _projectType,
                DynamicStagePatterns = _dynamicStagePatterns ?? new List<DynamicStagePattern>()
            };
        }
    }
    
    #endregion
    
    #region StageData Builder
    
    public static StageDataBuilder CreateStageData() => new();
    
    public class StageDataBuilder
    {
        private int _stageIndex = 0;
        private DateTime _stageDate = DateTime.UtcNow.AddDays(10);
        private bool _isDynamic = false;
        private List<StageDataTrx> _items = new();
        
        public StageDataBuilder WithStageIndex(int index)
        {
            _stageIndex = index;
            return this;
        }
        
        public StageDataBuilder WithStageDate(DateTime date)
        {
            _stageDate = date;
            return this;
        }
        
        public StageDataBuilder WithIsDynamic(bool isDynamic)
        {
            _isDynamic = isDynamic;
            return this;
        }
        
        public StageDataBuilder WithItems(List<StageDataTrx> items)
        {
            _items = items;
            return this;
        }
        
        public StageDataBuilder AddItem(StageDataTrx item)
        {
            _items.Add(item);
            return this;
        }
        
        public StageData Build()
        {
            return new StageData
            {
                StageIndex = _stageIndex,
                StageDate = _stageDate,
                IsDynamic = _isDynamic,
                Items = _items
            };
        }
    }
    
    #endregion
    
    #region StageDataTrx Builder
    
    public static StageDataTrxBuilder CreateStageDataTrx() => new();
    
    public class StageDataTrxBuilder
    {
        private string _trxId = Encoders.Hex.EncodeData(RandomUtils.GetBytes(32));
        private int _outputIndex = 2;
        private string _outputAddress = "tb1qtest";
        private int _stageIndex = 0;
        private long _amount = Money.Coins(1).Satoshi;
        private bool _isSpent = false;
        private string _spentType = "";
        private string _investorPublicKey = Encoders.Hex.EncodeData(RandomUtils.GetBytes(33));
        
        public StageDataTrxBuilder WithTrxId(string trxId)
        {
            _trxId = trxId;
            return this;
        }
        
        public StageDataTrxBuilder WithOutputIndex(int index)
        {
            _outputIndex = index;
            return this;
        }
        
        public StageDataTrxBuilder WithStageIndex(int index)
        {
            _stageIndex = index;
            return this;
        }
        
        public StageDataTrxBuilder WithAmount(long satoshis)
        {
            _amount = satoshis;
            return this;
        }
        
        public StageDataTrxBuilder AsSpent(string spentType)
        {
            _isSpent = true;
            _spentType = spentType;
            return this;
        }
        
        public StageDataTrxBuilder WithInvestorPublicKey(string pubKey)
        {
            _investorPublicKey = pubKey;
            return this;
        }
        
        public StageDataTrx Build()
        {
            return new StageDataTrx
            {
                Trxid = _trxId,
                Outputindex = _outputIndex,
                OutputAddress = _outputAddress,
                StageIndex = _stageIndex,
                Amount = _amount,
                IsSpent = _isSpent,
                SpentType = _spentType,
                InvestorPublicKey = _investorPublicKey
            };
        }
    }
    
    #endregion
    
    #region ProjectInvestment Builder
    
    public static ProjectInvestmentBuilder CreateProjectInvestment() => new();
    
    public class ProjectInvestmentBuilder
    {
        private string _transactionId = Encoders.Hex.EncodeData(RandomUtils.GetBytes(32));
        private string _investorPublicKey = Encoders.Hex.EncodeData(RandomUtils.GetBytes(33));
        private long _totalAmount = Money.Coins(1).Satoshi;
        
        public ProjectInvestmentBuilder WithTransactionId(string txId)
        {
            _transactionId = txId;
            return this;
        }
        
        public ProjectInvestmentBuilder WithInvestorPublicKey(string pubKey)
        {
            _investorPublicKey = pubKey;
            return this;
        }
        
        public ProjectInvestmentBuilder WithTotalAmount(long satoshis)
        {
            _totalAmount = satoshis;
            return this;
        }
        
        public ProjectInvestment Build()
        {
            return new ProjectInvestment
            {
                TransactionId = _transactionId,
                InvestorPublicKey = _investorPublicKey,
                TotalAmount = _totalAmount
            };
        }
    }
    
    #endregion
    
    #region QueryTransaction Builder
    
    public static QueryTransactionBuilder CreateQueryTransaction() => new();
    
    public class QueryTransactionBuilder
    {
        private string _transactionId = Encoders.Hex.EncodeData(RandomUtils.GetBytes(32));
        private List<QueryTransactionOutput> _outputs = new();
        private List<QueryTransactionInput> _inputs = new();
        
        public QueryTransactionBuilder WithTransactionId(string txId)
        {
            _transactionId = txId;
            return this;
        }
        
        public QueryTransactionBuilder AddOutput(int index, long balance, string? spentInTransaction = null)
        {
            _outputs.Add(new QueryTransactionOutput
            {
                Index = index,
                Balance = balance,
                SpentInTransaction = spentInTransaction
            });
            return this;
        }
        
        public QueryTransactionBuilder AddInput(string inputTxId, int inputIndex, string witScript = "")
        {
            _inputs.Add(new QueryTransactionInput
            {
                InputTransactionId = inputTxId,
                InputIndex = inputIndex,
                WitScript = witScript
            });
            return this;
        }
        
        public QueryTransaction Build()
        {
            return new QueryTransaction
            {
                TransactionId = _transactionId,
                Outputs = _outputs,
                Inputs = _inputs
            };
        }
    }
    
    #endregion
    
    #region InvestmentSpendingLookup Builder
    
    public static InvestmentSpendingLookupBuilder CreateInvestmentSpendingLookup() => new();
    
    public class InvestmentSpendingLookupBuilder
    {
        private string _projectId = Encoders.Hex.EncodeData(RandomUtils.GetBytes(32));
        private string _transactionId = Encoders.Hex.EncodeData(RandomUtils.GetBytes(32));
        private string? _endOfProjectTxId;
        private string? _recoveryTxId;
        private string? _recoveryReleaseTxId;
        private string? _unfundedReleaseTxId;
        private long _amountInRecovery = 0;
        
        public InvestmentSpendingLookupBuilder WithProjectId(string projectId)
        {
            _projectId = projectId;
            return this;
        }
        
        public InvestmentSpendingLookupBuilder WithTransactionId(string txId)
        {
            _transactionId = txId;
            return this;
        }
        
        public InvestmentSpendingLookupBuilder WithEndOfProjectTransaction(string txId)
        {
            _endOfProjectTxId = txId;
            return this;
        }
        
        public InvestmentSpendingLookupBuilder WithRecoveryTransaction(string txId, long amount)
        {
            _recoveryTxId = txId;
            _amountInRecovery = amount;
            return this;
        }
        
        public InvestmentSpendingLookupBuilder WithRecoveryReleaseTransaction(string txId)
        {
            _recoveryReleaseTxId = txId;
            return this;
        }
        
        public InvestmentSpendingLookup Build()
        {
            return new InvestmentSpendingLookup
            {
                ProjectIdentifier = _projectId,
                TransactionId = _transactionId,
                EndOfProjectTransactionId = _endOfProjectTxId,
                RecoveryTransactionId = _recoveryTxId,
                RecoveryReleaseTransactionId = _recoveryReleaseTxId,
                UnfundedReleaseTransactionId = _unfundedReleaseTxId,
                AmountInRecovery = _amountInRecovery
            };
        }
    }
    
    #endregion
}
