namespace NEventStore.Persistence.AcceptanceTests
{
	using System;
	using System.Data;
	using System.Data.Common;
	using System.Diagnostics;

	using NEventStore.Contrib.Persistence;

	internal class FirebirdInProcessConnectionFactory : IConnectionFactory
	{
		private readonly string _connectionString;

		private DbProviderFactory _dbProviderFactory;

		public FirebirdInProcessConnectionFactory(string connectionString)
		{
			this._connectionString = connectionString;
			this._dbProviderFactory = DbProviderFactories.GetFactory("FirebirdSql");
		}

		public IDbConnection Open()
		{
			DbConnection connection = this._dbProviderFactory.CreateConnection();
			Debug.Assert(connection != null, "connection != null");
			connection.ConnectionString = this._connectionString;
			try
			{
				connection.Open();
			}
			catch (Exception e)
			{
				throw new StorageUnavailableException(e.Message, e);
			}
			return connection;
		}

		public Type GetDbProviderFactoryType()
		{
			return this._dbProviderFactory.GetType();
		}
	}
}