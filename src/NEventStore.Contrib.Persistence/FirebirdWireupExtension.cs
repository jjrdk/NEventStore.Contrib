namespace NEventStore.Contrib.Persistence
{
	/// <summary>
    /// Extensions which allows usign firebird-specific Persistence due to the existing problems with sending more than one statement in a single command.
    /// </summary>
    public static class FirebirdWireupExtension
    {
        /// <summary>
        /// Extension method which allows using the FirebirdSqlPersistenceEngine instead of SqlPersistenceEngine.
        /// This Engine sends statements in different commands.
        /// </summary>
        /// <param name="wireup">The wireup.</param>
        /// <param name="connectionName">Name of the connection.</param>
        /// <param name="initialize">if set to <c>true</c> [initialize].</param>
        /// <returns>FirebirdSqlPersistenceWireup.</returns>
        public static FirebirdSqlPersistenceWireup UsingFirebirdPersistence(this Wireup wireup, string connectionName, bool initialize = false)
        {
            var factory = new ConfigurationConnectionFactory(connectionName);
            FirebirdSqlPersistenceWireup wire = new FirebirdSqlPersistenceWireup(wireup, factory);

            if (initialize)
            {
                wire.InitializeStorageEngine();
            }

            return wire;
        }

        /// <summary>
        /// Usings the firebird connection string.
        /// </summary>
        /// <param name="wireup">The wireup.</param>
        /// <param name="name">The name.</param>
        /// <param name="connectionString">The connection string.</param>
        /// <param name="providerName">Name of the provider.</param>
        /// <param name="initialize">if set to <c>true</c> [initialize].</param>
        /// <returns>The FirebirdSqlPersistenceWireup instance properly configured.</returns>
        public static FirebirdSqlPersistenceWireup UsingFirebirdConnectionString(this Wireup wireup, string name, string connectionString, string providerName, bool initialize = false)
        {
            var connectionFactory = new ConfigurationConnectionFactory(name, providerName, connectionString);
            var persistence = new FirebirdSqlPersistenceWireup(wireup, connectionFactory);
            if (initialize)
            {
                persistence.InitializeStorageEngine();
            }

            return persistence;
        }
    }
}
