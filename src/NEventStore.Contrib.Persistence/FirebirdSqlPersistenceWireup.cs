namespace NEventStore.Contrib.Persistence
{
	using System;
	using System.Security.Cryptography;
	using System.Transactions;

	using NEventStore.Logging;
	using NEventStore.Serialization;

	/// <summary>
	/// Class FirebirdSqlPersistenceWireup. Allows the usage of the FirebirdSqlPersistenceFactory which sends different statements in different commands to the database.
	/// This is due to a problem with the .NET Provider.
	/// </summary>
	public class FirebirdSqlPersistenceWireup : PersistenceWireup
	{
		private const int DefaultPageSize = 512;
		private static readonly ILog Logger = LogFactory.BuildLogger(typeof(FirebirdSqlPersistenceWireup));
		private int _pageSize = DefaultPageSize;

		/// <summary>
		/// Initializes a new instance of the <see cref="FirebirdSqlPersistenceWireup"/> class.
		/// </summary>
		/// <param name="wireup">The wireup.</param>
		/// <param name="connectionFactory">The connection factory.</param>
		public FirebirdSqlPersistenceWireup(Wireup wireup, IConnectionFactory connectionFactory)
			: base(wireup)
		{
			this.Container.Register<IContribSqlDialect>(c => null); // auto-detect
			this.Container.Register<IContribStreamIdHasher>(c => new StreamIdHasher<SHA1>());

			this.Container.Register(c => new FirebirdSqlPersistenceFactory(
				connectionFactory,
				c.Resolve<ISerialize>(),
				c.Resolve<IContribSqlDialect>(),
				c.Resolve<IContribStreamIdHasher>(),
				c.Resolve<TransactionScopeOption>(),
				this._pageSize).Build());
		}

		/// <summary>
		/// Withes the dialect.
		/// </summary>
		/// <param name="instance">The instance.</param>
		/// <returns>FirebirdSqlPersistenceWireup.</returns>
		public virtual FirebirdSqlPersistenceWireup WithDialect(IContribSqlDialect instance)
		{
			this.Container.Register(instance);
			return this;
		}

		/// <summary>
		/// Pages the every.
		/// </summary>
		/// <param name="records">The records.</param>
		/// <returns>FirebirdSqlPersistenceWireup.</returns>
		public virtual FirebirdSqlPersistenceWireup PageEvery(int records)
		{
			this._pageSize = records;
			return this;
		}

		/// <summary>
		/// Withes the stream identifier hasher.
		/// </summary>
		/// <param name="instance">The instance.</param>
		/// <returns>FirebirdSqlPersistenceWireup.</returns>
		public virtual FirebirdSqlPersistenceWireup WithStreamIdHasher(IContribStreamIdHasher instance)
		{
			this.Container.Register(instance);
			return this;
		}

		/// <summary>
		/// Withes the stream identifier hasher.
		/// </summary>
		/// <param name="getStreamIdHash">The get stream identifier hash.</param>
		/// <returns>FirebirdSqlPersistenceWireup.</returns>
		public virtual FirebirdSqlPersistenceWireup WithStreamIdHasher(Func<string, string> getStreamIdHash)
		{
			return this.WithStreamIdHasher(new DelegateStreamIdHasher(getStreamIdHash));
		}
	}
}
