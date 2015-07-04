namespace NEventStore.Contrib.Persistence
{
	using System;
	using System.Collections.Generic;
	using System.Configuration;
	using System.Data;
	using System.Data.Common;
	using System.Linq;

	using NEventStore.Logging;
	using NEventStore.Persistence;

	public class ConfigurationConnectionFactory : IConnectionFactory
    {
        private const string DefaultConnectionName = "NEventStore";

        private static readonly ILog Logger = LogFactory.BuildLogger(typeof (ConfigurationConnectionFactory));

        private static readonly IDictionary<string, ConnectionStringSettings> CachedSettings =
            new Dictionary<string, ConnectionStringSettings>();

        private static readonly IDictionary<string, DbProviderFactory> CachedFactories =
            new Dictionary<string, DbProviderFactory>();

        private readonly string _connectionName;
        private readonly ConnectionStringSettings _connectionStringSettings;

        public ConfigurationConnectionFactory(string connectionName)
        {
            this._connectionName = connectionName ?? DefaultConnectionName;
            Logger.Debug(Messages.ConfiguringConnections, this._connectionName);
        }

        public ConfigurationConnectionFactory(string connectionName, string providerName, string connectionString)
            : this(connectionName)
        {
            this._connectionStringSettings = new ConnectionStringSettings(this._connectionName, connectionString, providerName);
        }

        public virtual ConnectionStringSettings Settings
        {
            get { return this.GetConnectionStringSettings(this._connectionName); }
        }

        public virtual IDbConnection Open()
        {
            Logger.Verbose(Messages.OpeningMasterConnection, this._connectionName);
            return this.Open(this._connectionName);
        }

        public Type GetDbProviderFactoryType()
        {
            DbProviderFactory factory = this.GetFactory(this.Settings);
            return factory.GetType();
        }

        protected virtual IDbConnection Open(string connectionName)
        {
            ConnectionStringSettings setting = this.GetSetting(connectionName);
            string connectionString = setting.ConnectionString;
            return new ConnectionScope(connectionString, () => this.Open(connectionString, setting));
        }

        protected virtual IDbConnection Open(string connectionString, ConnectionStringSettings setting)
        {
            DbProviderFactory factory = this.GetFactory(setting);
            DbConnection connection = factory.CreateConnection();
            if (connection == null)
            {
                throw new ConfigurationErrorsException(Messages.BadConnectionName);
            }

            connection.ConnectionString = connectionString;

            try
            {
                Logger.Verbose(Messages.OpeningConnection, setting.Name);
                connection.Open();
            }
            catch (Exception e)
            {
                Logger.Warn(Messages.OpenFailed, setting.Name);
                throw new StorageUnavailableException(e.Message, e);
            }

            return connection;
        }

        protected virtual ConnectionStringSettings GetSetting(string connectionName)
        {
            lock (CachedSettings)
            {
                ConnectionStringSettings setting;
                if (CachedSettings.TryGetValue(connectionName, out setting))
                {
                    return setting;
                }

                setting = this.GetConnectionStringSettings(connectionName);
                return CachedSettings[connectionName] = setting;
            }
        }

        protected virtual DbProviderFactory GetFactory(ConnectionStringSettings setting)
        {
            lock (CachedFactories)
            {
                DbProviderFactory factory;
                if (CachedFactories.TryGetValue(setting.Name, out factory))
                {
                    return factory;
                }
                factory = DbProviderFactories.GetFactory(setting.ProviderName);
                Logger.Debug(Messages.DiscoveredConnectionProvider, setting.Name, factory.GetType());
                return CachedFactories[setting.Name] = factory;
            }
        }

        protected virtual ConnectionStringSettings GetConnectionStringSettings(string connectionName)
        {
            Logger.Debug(Messages.DiscoveringConnectionSettings, connectionName);

            ConnectionStringSettings settings = this._connectionStringSettings ?? ConfigurationManager.ConnectionStrings
                                                                    .Cast<ConnectionStringSettings>()
                                                                    .FirstOrDefault(x => x.Name == connectionName);

            if (settings == null)
            {
                throw new ConfigurationErrorsException(Messages.ConnectionNotFound.FormatWith(connectionName));
            }

            if ((settings.ConnectionString ?? string.Empty).Trim().Length == 0)
            {
                throw new ConfigurationErrorsException(Messages.MissingConnectionString.FormatWith(connectionName));
            }

            if ((settings.ProviderName ?? string.Empty).Trim().Length == 0)
            {
                throw new ConfigurationErrorsException(Messages.MissingProviderName.FormatWith(connectionName));
            }

            return settings;
        }
    }
}