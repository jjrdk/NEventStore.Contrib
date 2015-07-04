namespace NEventStore.Contrib.Persistence.SqlDialects
{
	using System.Data;

	public delegate void NextPageDelegate(IDbCommand command, IDataRecord current);
}