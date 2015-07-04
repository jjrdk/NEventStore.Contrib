namespace NEventStore.Contrib.Persistence
{
	using System;
	using System.Collections.Generic;
	using System.Data;

	using NEventStore.Contrib.Persistence.SqlDialects;

	public interface IContribDbStatement : IDisposable
    {
        int PageSize { get; set; }

        void AddParameter(string name, object value, DbType? parameterType = null);

        int ExecuteNonQuery(string commandText);

        int ExecuteWithoutExceptions(string commandText);

        object ExecuteScalar(string commandText);

        IEnumerable<IDataRecord> ExecuteWithQuery(string queryText);

        IEnumerable<IDataRecord> ExecutePagedQuery(string queryText, NextPageDelegate nextpage);
    }
}