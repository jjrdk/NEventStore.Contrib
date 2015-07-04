namespace NEventStore.Contrib.Persistence.SqlDialects
{
	using System;
	using System.Collections;
	using System.Collections.Generic;
	using System.Data;
	using System.Transactions;

	using NEventStore.Logging;
	using NEventStore.Persistence;

	public class PagedEnumerationCollection : IEnumerable<IDataRecord>, IEnumerator<IDataRecord>
    {
        private static readonly ILog Logger = LogFactory.BuildLogger(typeof (PagedEnumerationCollection));
        private readonly IDbCommand _command;
        private readonly IContribSqlDialect _dialect;
        private readonly IEnumerable<IDisposable> _disposable = new IDisposable[] {};
        private readonly NextPageDelegate _nextpage;
        private readonly int _pageSize;
        private readonly TransactionScope _scope;

        private IDataRecord _current;
        private bool _disposed;
        private int _position;
        private IDataReader _reader;

        public PagedEnumerationCollection(
            TransactionScope scope,
            IContribSqlDialect dialect,
            IDbCommand command,
            NextPageDelegate nextpage,
            int pageSize,
            params IDisposable[] disposable)
        {
            this._scope = scope;
            this._dialect = dialect;
            this._command = command;
            this._nextpage = nextpage;
            this._pageSize = pageSize;
            this._disposable = disposable ?? this._disposable;
        }

        public virtual IEnumerator<IDataRecord> GetEnumerator()
        {
            if (this._disposed)
            {
                throw new ObjectDisposedException(Messages.ObjectAlreadyDisposed);
            }

            return this;
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return this.GetEnumerator();
        }

        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        bool IEnumerator.MoveNext()
        {
            if (this._disposed)
            {
                throw new ObjectDisposedException(Messages.ObjectAlreadyDisposed);
            }

            if (this.MoveToNextRecord())
            {
                return true;
            }

            Logger.Verbose(Messages.QueryCompleted);
            return false;
        }

        public virtual void Reset()
        {
            throw new NotSupportedException("Forward-only readers.");
        }

        public virtual IDataRecord Current
        {
            get
            {
                if (this._disposed)
                {
                    throw new ObjectDisposedException(Messages.ObjectAlreadyDisposed);
                }

                return this._current = this._reader;
            }
        }

        object IEnumerator.Current
        {
            get { return ((IEnumerator<IDataRecord>) this).Current; }
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposing || this._disposed)
            {
                return;
            }

            this._disposed = true;
            this._position = 0;
            this._current = null;

            if (this._reader != null)
            {
                this._reader.Dispose();
            }

            this._reader = null;

            if (this._command != null)
            {
                this._command.Dispose();
            }

            // queries do not modify state and thus calling Complete() on a so-called 'failed' query only
            // allows any outer transaction scope to decide the fate of the transaction
            if (this._scope != null)
            {
                this._scope.Complete(); // caller will dispose scope.
            }

            foreach (var dispose in this._disposable)
            {
                dispose.Dispose();
            }
        }

        private bool MoveToNextRecord()
        {
            if (this._pageSize > 0 && this._position >= this._pageSize)
            {
                this._command.SetParameter(this._dialect.Skip, this._position);
                this._nextpage(this._command, this._current);
            }

            this._reader = this._reader ?? this.OpenNextPage();

            if (this._reader.Read())
            {
                return this.IncrementPosition();
            }

            if (!this.PagingEnabled())
            {
                return false;
            }

            if (!this.PageCompletelyEnumerated())
            {
                return false;
            }

            Logger.Verbose(Messages.EnumeratedRowCount, this._position);
            this._reader.Dispose();
            this._reader = this.OpenNextPage();

            if (this._reader.Read())
            {
                return this.IncrementPosition();
            }

            return false;
        }

        private bool IncrementPosition()
        {
            this._position++;
            return true;
        }

        private bool PagingEnabled()
        {
            return this._pageSize > 0;
        }

        private bool PageCompletelyEnumerated()
        {
            return this._position > 0 && 0 == this._position%this._pageSize;
        }

        private IDataReader OpenNextPage()
        {
            try
            {
                return this._command.ExecuteReader();
            }
            catch (Exception e)
            {
                Logger.Debug(Messages.EnumerationThrewException, e.GetType());
                throw new StorageUnavailableException(e.Message, e);
            }
        }
    }
}