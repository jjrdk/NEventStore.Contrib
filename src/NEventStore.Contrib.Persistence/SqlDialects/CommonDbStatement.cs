namespace NEventStore.Contrib.Persistence.SqlDialects
{
	using System;
	using System.Collections.Generic;
	using System.Data;
	using System.Transactions;

	using NEventStore.Logging;

	public class CommonDbStatement : IContribDbStatement
    {
        private const int InfinitePageSize = 0;
        private static readonly ILog Logger = LogFactory.BuildLogger(typeof (CommonDbStatement));
        private readonly IDbConnection _connection;
        private readonly IContribSqlDialect _dialect;
        private readonly TransactionScope _scope;
        private readonly IDbTransaction _transaction;

        public CommonDbStatement(
            IContribSqlDialect dialect,
            TransactionScope scope,
            IDbConnection connection,
            IDbTransaction transaction)
        {
            this.Parameters = new Dictionary<string, Tuple<object, DbType?>>();

            this._dialect = dialect;
            this._scope = scope;
            this._connection = connection;
            this._transaction = transaction;
        }

        protected IDictionary<string, Tuple<object, DbType?>> Parameters { get; private set; }

        protected IContribSqlDialect Dialect
        {
            get { return this._dialect; }
        }

        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        public virtual int PageSize { get; set; }

        public virtual void AddParameter(string name, object value, DbType? parameterType = null)
        {
            Logger.Debug(Messages.AddingParameter, name);
            this.Parameters[name] = Tuple.Create(this._dialect.CoalesceParameterValue(value), parameterType);
        }

        public virtual int ExecuteWithoutExceptions(string commandText)
        {
            try
            {
                return this.ExecuteNonQuery(commandText);
            }
            catch (Exception)
            {
                Logger.Debug(Messages.ExceptionSuppressed);
                return 0;
            }
        }

        public virtual int ExecuteNonQuery(string commandText)
        {
            try
            {
                using (IDbCommand command = this.BuildCommand(commandText))
                {
                    return command.ExecuteNonQuery();
                }
            }
            catch (Exception e)
            {
                if (this._dialect.IsDuplicate(e))
                {
                    throw new UniqueKeyViolationException(e.Message, e);
                }

                throw;
            }
        }

        public virtual object ExecuteScalar(string commandText)
        {
            try
            {
                using (IDbCommand command = this.BuildCommand(commandText))
                {
                    return command.ExecuteScalar();
                }
            }
            catch (Exception e)
            {
                if (this._dialect.IsDuplicate(e))
                {
                    throw new UniqueKeyViolationException(e.Message, e);
                }
                throw;
            }
        }

        public virtual IEnumerable<IDataRecord> ExecuteWithQuery(string queryText)
        {
            return this.ExecuteQuery(queryText, (query, latest) => { }, InfinitePageSize);
        }

        public virtual IEnumerable<IDataRecord> ExecutePagedQuery(string queryText, NextPageDelegate nextpage)
        {
            int pageSize = this._dialect.CanPage ? this.PageSize : InfinitePageSize;
            if (pageSize > 0)
            {
                Logger.Verbose(Messages.MaxPageSize, pageSize);
                this.Parameters.Add(this._dialect.Limit, Tuple.Create((object) pageSize, (DbType?) null));
            }

            return this.ExecuteQuery(queryText, nextpage, pageSize);
        }

        protected virtual void Dispose(bool disposing)
        {
            Logger.Verbose(Messages.DisposingStatement);

            if (this._transaction != null)
            {
                this._transaction.Dispose();
            }

            if (this._connection != null)
            {
                this._connection.Dispose();
            }

            if (this._scope != null)
            {
                this._scope.Dispose();
            }
        }

        protected virtual IEnumerable<IDataRecord> ExecuteQuery(string queryText, NextPageDelegate nextpage, int pageSize)
        {
            this.Parameters.Add(this._dialect.Skip, Tuple.Create((object) 0, (DbType?) null));
            IDbCommand command = this.BuildCommand(queryText);

            try
            {
                return new PagedEnumerationCollection(this._scope, this._dialect, command, nextpage, pageSize, this);
            }
            catch (Exception)
            {
                command.Dispose();
                throw;
            }
        }

        protected virtual IDbCommand BuildCommand(string statement)
        {
            Logger.Verbose(Messages.CreatingCommand);
            IDbCommand command = this._connection.CreateCommand();

            int timeout = 0;
            if( int.TryParse( System.Configuration.ConfigurationManager.AppSettings["NEventStore.SqlCommand.Timeout"], out timeout ) ) 
            {
              command.CommandTimeout = timeout;
            }

            command.Transaction = this._transaction;
            command.CommandText = statement;

            Logger.Verbose(Messages.ClientControlledTransaction, this._transaction != null);
            Logger.Verbose(Messages.CommandTextToExecute, statement);

            this.BuildParameters(command);

            return command;
        }

        protected virtual void BuildParameters(IDbCommand command)
        {
            foreach (var item in this.Parameters)
            {
                this.BuildParameter(command, item.Key, item.Value.Item1, item.Value.Item2);
            }
        }

        protected virtual void BuildParameter(IDbCommand command, string name, object value, DbType? dbType)
        {
            IDbDataParameter parameter = command.CreateParameter();
            parameter.ParameterName = name;
            this.SetParameterValue(parameter, value, dbType);

            Logger.Verbose(Messages.BindingParameter, name, parameter.Value);
            command.Parameters.Add(parameter);
        }

        protected virtual void SetParameterValue(IDataParameter param, object value, DbType? type)
        {
            param.Value = value ?? DBNull.Value;
            param.DbType = type ?? (value == null ? DbType.Binary : param.DbType);
        }
    }
}