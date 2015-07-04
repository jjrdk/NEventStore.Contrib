namespace NEventStore.Contrib.Persistence
{
	using System;
	using System.Threading;
	using System.Web;

	using NEventStore.Logging;

	public class ThreadScope<T> : IDisposable where T : class
    {
        private readonly HttpContext _context = HttpContext.Current;
        private readonly T _current;
        private readonly ILog _logger = LogFactory.BuildLogger(typeof (ThreadScope<T>));
        private readonly bool _rootScope;
        private readonly string _threadKey;
        private bool _disposed;

        public ThreadScope(string key, Func<T> factory)
        {
            this._threadKey = typeof (ThreadScope<T>).Name + ":[{0}]".FormatWith(key ?? string.Empty);

            T parent = this.Load();
            this._rootScope = parent == null;
            this._logger.Debug(Messages.OpeningThreadScope, this._threadKey, this._rootScope);

            this._current = parent ?? factory();

            if (this._current == null)
            {
                throw new ArgumentException(Messages.BadFactoryResult, "factory");
            }

            if (this._rootScope)
            {
                this.Store(this._current);
            }
        }

        public T Current
        {
            get { return this._current; }
        }

        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposing || this._disposed)
            {
                return;
            }

            this._logger.Debug(Messages.DisposingThreadScope, this._rootScope);
            this._disposed = true;
            if (!this._rootScope)
            {
                return;
            }

            this._logger.Verbose(Messages.CleaningRootThreadScope);
            this.Store(null);

            var resource = this._current as IDisposable;
            if (resource == null)
            {
                return;
            }

            this._logger.Verbose(Messages.DisposingRootThreadScopeResources);
            resource.Dispose();
        }

        private T Load()
        {
            if (this._context != null)
            {
                return this._context.Items[this._threadKey] as T;
            }

            return Thread.GetData(Thread.GetNamedDataSlot(this._threadKey)) as T;
        }

        private void Store(T value)
        {
            if (this._context != null)
            {
                this._context.Items[this._threadKey] = value;
            }
            else
            {
                Thread.SetData(Thread.GetNamedDataSlot(this._threadKey), value);
            }
        }
    }
}