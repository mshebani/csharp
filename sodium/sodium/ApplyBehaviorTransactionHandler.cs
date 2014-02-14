namespace sodium
{
    public class ApplyBehaviorTransactionHandler<TA, TB> : ITransactionHandler<IFunction<TA, TB>>
    {
        private readonly IHandler<Transaction> _h;

        public ApplyBehaviorTransactionHandler(IHandler<Transaction> h)
        {
            _h = h;
        }

        public void Run(Transaction trans, IFunction<TA, TB> a)
        {
            _h.Run(trans);
        }
    }
}