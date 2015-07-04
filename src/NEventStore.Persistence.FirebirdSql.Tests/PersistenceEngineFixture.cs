namespace NEventStore.Persistence.AcceptanceTests
{
	using System;
	using System.Security.Cryptography;

	using FirebirdSql.Data.FirebirdClient;

	using NEventStore.Persistence.Sql;
	using NEventStore.Persistence.Sql.SqlDialects;
	using NEventStore.Serialization;

	public partial class PersistenceEngineFixture
	{
		const string ConnectionString = "User=SYSDBA;Password=doesntmatter;Database=neventstore.fdb;DataSource=localhost;Port=3050;Dialect=3;Charset=UTF8;Role=;Connection lifetime=15;Pooling=true;MinPoolSize=0;MaxPoolSize=50;Packet Size=8192;ServerType=1;";

		public PersistenceEngineFixture()
		{
			FbConnection.ClearAllPools();
			FbConnection.CreateDatabase(ConnectionString, true);

			_createPersistence =
				pageSize =>
				new FirebirdSqlPersistenceFactory(
					new FirebirdInProcessConnectionFactory(ConnectionString),
					new BinarySerializer(),
					new FirebirdSqlDialect(),
					pageSize: pageSize,
					streamIdHasher: new StreamIdHasher<SHA256>()).Build();
		}
	}
}