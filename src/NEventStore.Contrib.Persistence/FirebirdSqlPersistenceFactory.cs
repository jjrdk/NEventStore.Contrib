namespace NEventStore.Persistence.Sql
{
	using System;
	using System.Transactions;

	using NEventStore.Persistence.Sql.SqlDialects;
	using NEventStore.Serialization;

	public class FirebirdSqlPersistenceFactory : IPersistenceFactory
	{
		private readonly TransactionScopeOption _transactionScopeOption;
		private const int DefaultPageSize = 128;
		private readonly IConnectionFactory _connectionFactory;
		private readonly ISqlDialect _dialect;
		private readonly TransactionScopeOption _scopeOption;
		private readonly ISerialize _serializer;
		private readonly IStreamIdHasher _streamIdHasher;
		private readonly int _pagesize = 128;

		public FirebirdSqlPersistenceFactory(string connectionName, ISerialize serializer)
			: this(new ConfigurationConnectionFactory(connectionName), serializer, new FirebirdSqlDialect(), new Sha1StreamIdHasher())
		{
		}

		public FirebirdSqlPersistenceFactory(IConnectionFactory factory, ISerialize serializer, ISqlDialect dialect, IStreamIdHasher streamIdHasher = null, TransactionScopeOption scopeOption = TransactionScopeOption.Suppress, int pageSize = 128)
		{
			_connectionFactory = factory;
			_dialect = dialect;
			_transactionScopeOption = scopeOption;
			_serializer = serializer;
			_scopeOption = scopeOption;
			_streamIdHasher = streamIdHasher ?? new Sha1StreamIdHasher();
			_pagesize = pageSize;
		}

		public IPersistStreams Build()
		{
			return new FirebirdPersistenceEngine(_connectionFactory, _dialect, _serializer, _transactionScopeOption, _pagesize, _streamIdHasher);
		}
	}
}
