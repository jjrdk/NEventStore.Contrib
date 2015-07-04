namespace NEventStore.Persistence.Sql
{
	using System;
	using System.Security.Cryptography;
	using System.Transactions;

	using NEventStore.Persistence.Sql.SqlDialects;
	using NEventStore.Serialization;

	public class FirebirdSqlPersistenceFactory : IPersistenceFactory
	{
		private readonly TransactionScopeOption _transactionScopeOption;
		private const int DefaultPageSize = 128;
		private readonly IConnectionFactory _connectionFactory;
		private readonly IContribSqlDialect _dialect;
		private readonly TransactionScopeOption _scopeOption;
		private readonly ISerialize _serializer;
		private readonly IContribStreamIdHasher _streamIdHasher;
		private readonly int _pagesize = 128;

		public FirebirdSqlPersistenceFactory(IConnectionFactory factory, ISerialize serializer, IContribSqlDialect dialect, IContribStreamIdHasher streamIdHasher = null, TransactionScopeOption scopeOption = TransactionScopeOption.Suppress, int pageSize = DefaultPageSize)
		{
			_connectionFactory = factory;
			_dialect = dialect;
			_transactionScopeOption = scopeOption;
			_serializer = serializer;
			_scopeOption = scopeOption;
			_streamIdHasher = streamIdHasher ?? new StreamIdHasher<SHA1>();
			_pagesize = pageSize;
		}

		public IPersistStreams Build()
		{
			return new FirebirdSqlPersistenceEngine(_connectionFactory, _dialect, _serializer, _transactionScopeOption, _pagesize, _streamIdHasher);
		}
	}
}
