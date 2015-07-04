namespace NEventStore.Contrib.Persistence
{
	using System.Security.Cryptography;
	using System.Transactions;

	using NEventStore.Persistence;
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
			this._connectionFactory = factory;
			this._dialect = dialect;
			this._transactionScopeOption = scopeOption;
			this._serializer = serializer;
			this._scopeOption = scopeOption;
			this._streamIdHasher = streamIdHasher ?? new StreamIdHasher<SHA1>();
			this._pagesize = pageSize;
		}

		public IPersistStreams Build()
		{
			return new FirebirdSqlPersistenceEngine(this._connectionFactory, this._dialect, this._serializer, this._transactionScopeOption, this._pagesize, this._streamIdHasher);
		}
	}
}
