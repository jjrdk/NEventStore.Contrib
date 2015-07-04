namespace NEventStore.Contrib.Persistence
{
	using System;
	using System.Reflection;
	using System.Security.Cryptography;
	using System.Text;

	public class StreamIdHasher<THash> : IContribStreamIdHasher
		where THash : HashAlgorithm
	{
		private MethodInfo _createMethod = typeof(THash).GetMethod("Create", BindingFlags.Public | BindingFlags.Static, null, CallingConventions.Any, Type.EmptyTypes, new ParameterModifier[0]);

		public string GetHash(string streamId)
		{
			var instance = (THash)this._createMethod.Invoke(null, null);
			var hash = instance.ComputeHash(Encoding.UTF8.GetBytes(streamId));

			return BitConverter.ToString(hash).Replace("-", string.Empty);
		}
	}
}