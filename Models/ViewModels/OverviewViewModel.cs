namespace RYT.Models.ViewModels
{
    public class OverviewViewModel
    {
        public decimal Balance { get; set; }
        public decimal AmountSent { get; set; }
        public decimal AmountReceived { get; set; }
        public string Status { get; set; }
        public List<FundingTransactionHistoryViewModel> MyFundings { get; set; } = new List<FundingTransactionHistoryViewModel>();
        public List<ReceivedTransactionsViewModel> MyReceivedTransactions { get; set; } = new List<ReceivedTransactionsViewModel>();
        public List<TransferTransactionHistoryViewModel> MyTransferTransactions { get; set; } = new List<TransferTransactionHistoryViewModel>();
        public List<SentTransactionViewModel> MySentTransactions { get; set; } = new List<SentTransactionViewModel>();

        public FundWalletVM FundingVM { get; set; }
    }
}
