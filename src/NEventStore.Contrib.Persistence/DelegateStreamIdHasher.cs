namespace NEventStore.Persistence.Sql
{
    using System;

    public class DelegateStreamIdHasher : IContribStreamIdHasher
    {
        private readonly Func<string, string> _getHash;

        public DelegateStreamIdHasher(Func<string, string> getHash)
        {
            if (getHash == null)
            {
                throw new ArgumentNullException("getHash");
            }
            _getHash = getHash;
        }

        public string GetHash(string streamId)
        {
            return _getHash(streamId);
        }
    }
}