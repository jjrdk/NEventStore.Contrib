namespace NEventStore.Contrib.Persistence
{
	using System;
	using System.Data;

	public interface IConnectionFactory
    {
        IDbConnection Open();

        Type GetDbProviderFactoryType();
    }
}