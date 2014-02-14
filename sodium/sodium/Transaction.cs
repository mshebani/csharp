namespace sodium
{
    using System;
    using System.Collections.Generic;

    public sealed class Transaction
    {
        // Coarse-grained lock that's held during the whole transaction.
        private static readonly Object TransactionLock = new Object();

        // Fine-grained lock that protects listeners and nodes.
        public static readonly Object ListenersLock = new Object();

        public bool NeedToRegeneratePriorityQueue = false;

        private readonly IPriorityQueue<Entry> _prioritizedQ = new PriorityQueue<Entry>();
        private readonly ISet<Entry> _entries = new HashSet<Entry>();
        private readonly List<IRunnable> _lastQ = new List<IRunnable>();
        private List<IRunnable> _postQ;
        private static Transaction _currentTransaction;

        /**
         * Run the specified code inside a single transaction.
         *
         * In most cases this is not needed, because all APIs will create their own
         * transaction automatically. It is useful where you want to run multiple
         * reactive operations atomically.
         */
        public static void Run(IRunnable code)
        {
            lock (TransactionLock)
            {
                // If we are already inside a transaction (which must be on the same
                // thread otherwise we wouldn't have acquired transactionLock), then
                // keep using that same transaction.
                var transWas = _currentTransaction;
                try
                {
                    if (_currentTransaction == null)
                        _currentTransaction = new Transaction();
                    code.Run();
                }
                finally
                {
                    if (transWas == null)
                        _currentTransaction.Close();
                    _currentTransaction = transWas;
                }
            }
        }

        public static void Run(IHandler<Transaction> code)
        {
            lock (TransactionLock)
            {
                // If we are already inside a transaction (which must be on the same
                // thread otherwise we wouldn't have acquired transactionLock), then
                // keep using that same transaction.
                var transWas = _currentTransaction;
                try
                {
                    if (_currentTransaction == null)
                        _currentTransaction = new Transaction();
                    code.Run(_currentTransaction);
                }
                finally
                {
                    if (transWas == null)
                        _currentTransaction.Close();
                    _currentTransaction = transWas;
                }
            }
        }

        public static TA Apply<TA>(IFunction<Transaction, TA> code)
        {
            lock (TransactionLock)
            {
                // If we are already inside a transaction (which must be on the same
                // thread otherwise we wouldn't have acquired transactionLock), then
                // keep using that same transaction.
                var transWas = _currentTransaction;
                try
                {
                    if (_currentTransaction == null)
                        _currentTransaction = new Transaction();
                    return code.Apply(_currentTransaction);
                }
                finally
                {
                    _currentTransaction.Close();
                    _currentTransaction = transWas;
                }
            }
        }

        public void Prioritized(Node rank, IHandler<Transaction> action)
        {
            var e = new Entry(rank, action);
            _prioritizedQ.Add(e);
            _entries.Add(e);
        }

        /**
         * Add an action to run after all prioritized() actions.
         */
        public void Last(IRunnable action)
        {
            _lastQ.Add(action);
        }

        /**
         * Add an action to run after all last() actions.
         */
        public void Post(IRunnable action)
        {
            if (_postQ == null)
                _postQ = new List<IRunnable>();
            _postQ.Add(action);
        }

        /**
         * If the priority queue has entries in it when we modify any of the nodes'
         * ranks, then we need to re-generate it to make sure it's up-to-date.
         */
        private void CheckRegeneratePriorityQueue()
        {
            if (NeedToRegeneratePriorityQueue)
            {
                RegeneratePriorityQueue();
                NeedToRegeneratePriorityQueue = false;
            }
        }

        private void RegeneratePriorityQueue()
        {
            _prioritizedQ.Clear();
            foreach (var e in _entries)
                _prioritizedQ.Add(e);
        }

        public void Close()
        {
            while (true)
            {
                CheckRegeneratePriorityQueue();
                if (_prioritizedQ.IsEmpty()) break;
                var e = _prioritizedQ.Remove();
                _entries.Remove(e);
                e.Action.Run(this);
            }
            foreach (var action in _lastQ)
                action.Run();
            _lastQ.Clear();
            if (_postQ != null)
            {
                foreach (var action in _postQ)
                    action.Run();
                _postQ.Clear();
            }
        }
    }
}