namespace NEventStore.Contrib.Persistence
{
	using System;
	using System.Collections.Generic;
	using System.Data;
	using System.Globalization;
	using System.Linq;
	using System.Text;
	using System.Threading;
	using System.Transactions;

	using NEventStore.Logging;
	using NEventStore.Persistence;
	using NEventStore.Serialization;

	public class FirebirdSqlPersistenceEngine : IPersistStreams
    {
        private static readonly ILog Logger = LogFactory.BuildLogger(typeof(FirebirdSqlPersistenceEngine));
        private static readonly DateTime EpochTime = new DateTime(1970, 1, 1);
        private readonly IConnectionFactory _connectionFactory;
        private readonly IContribSqlDialect _dialect;
        private readonly int _pageSize;
        private readonly TransactionScopeOption _scopeOption;
        private readonly ISerialize _serializer;
        private bool _disposed;
        private int _initialized;
        private readonly IContribStreamIdHasher _streamIdHasher;

        public FirebirdSqlPersistenceEngine(
            IConnectionFactory connectionFactory,
            IContribSqlDialect dialect,
            ISerialize serializer,
            TransactionScopeOption scopeOption,
            int pageSize,
            IContribStreamIdHasher streamIdHasher)
        {
            if (connectionFactory == null)
            {
                throw new ArgumentNullException("connectionFactory");
            }

            if (dialect == null)
            {
                throw new ArgumentNullException("dialect");
            }

            if (serializer == null)
            {
                throw new ArgumentNullException("serializer");
            }

            if (pageSize < 0)
            {
                throw new ArgumentException("pageSize");
            }

            if (streamIdHasher == null)
            {
                throw new ArgumentNullException("streamIdHasher");
            }

            this._connectionFactory = connectionFactory;
            this._dialect = dialect;
            this._serializer = serializer;
            this._scopeOption = scopeOption;
            this._pageSize = pageSize;
            this._streamIdHasher = new StreamIdHasherValidator(streamIdHasher);

            Logger.Debug(Messages.UsingScope, this._scopeOption.ToString());
        }

        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        public virtual void Initialize()
        {
            if (Interlocked.Increment(ref this._initialized) > 1)
            {
                return;
            }

            Logger.Debug(Messages.InitializingStorage);

            string[] statements = this._dialect.InitializeStorage.Split(new[] { "__" }, StringSplitOptions.RemoveEmptyEntries);

            StringBuilder builder = null;
            bool buildingSetTermStatement = false;

            foreach (string s in statements)
            {
                this.ExecuteCommand(statement => statement.ExecuteWithoutExceptions(s.Trim()));
            }
        }

        public virtual IEnumerable<ICommit> GetFrom(string bucketId, string streamId, int minRevision, int maxRevision)
        {
            Logger.Debug(Messages.GettingAllCommitsBetween, streamId, minRevision, maxRevision);
            streamId = this._streamIdHasher.GetHash(streamId);
            return this.ExecuteQuery(query =>
                {
                    string statement = this._dialect.GetCommitsFromStartingRevision;
                    query.AddParameter(this._dialect.BucketId, bucketId, DbType.AnsiString);
                    query.AddParameter(this._dialect.StreamId, streamId, DbType.AnsiString);
                    query.AddParameter(this._dialect.StreamRevision, minRevision);
                    query.AddParameter(this._dialect.MaxStreamRevision, maxRevision);
                    query.AddParameter(this._dialect.CommitSequence, 0);
                    return query
                        .ExecutePagedQuery(statement, this._dialect.NextPageDelegate)
                        .Select(x => x.GetCommit(this._serializer, this._dialect));

                    /* return query
                         .ExecutePagedQuery(statement, (q, r) => {})
                         .Select(x => x.GetCommit(_serializer, _dialect));*/
                });
        }

        public virtual IEnumerable<ICommit> GetFrom(string bucketId, DateTime start)
        {
            start = start.AddTicks(-(start.Ticks % TimeSpan.TicksPerSecond)); // Rounds down to the nearest second.
            start = start < EpochTime ? EpochTime : start;

            Logger.Debug(Messages.GettingAllCommitsFrom, start, bucketId);
            return this.ExecuteQuery(query =>
                {
                    string statement = this._dialect.GetCommitsFromInstant;
                    query.AddParameter(this._dialect.BucketId, bucketId, DbType.AnsiString);
                    query.AddParameter(this._dialect.CommitStamp, start);
                    return query.ExecutePagedQuery(statement, (q, r) => { })
                            .Select(x => x.GetCommit(this._serializer, this._dialect));

                });
        }

        public ICheckpoint GetCheckpoint(string checkpointToken)
        {
            return string.IsNullOrWhiteSpace(checkpointToken) ? null : LongCheckpoint.Parse(checkpointToken);
        }

        public virtual IEnumerable<ICommit> GetFromTo(string bucketId, DateTime start, DateTime end)
        {
            start = start.AddTicks(-(start.Ticks % TimeSpan.TicksPerSecond)); // Rounds down to the nearest second.
            start = start < EpochTime ? EpochTime : start;
            end = end < EpochTime ? EpochTime : end;

            Logger.Debug(Messages.GettingAllCommitsFromTo, start, end);
            return this.ExecuteQuery(query =>
                {
                    string statement = this._dialect.GetCommitsFromToInstant;
                    query.AddParameter(this._dialect.BucketId, bucketId, DbType.AnsiString);
                    query.AddParameter(this._dialect.CommitStampStart, start);
                    query.AddParameter(this._dialect.CommitStampEnd, end);
                    return query.ExecutePagedQuery(statement, (q, r) => { })
                        .Select(x => x.GetCommit(this._serializer, this._dialect));
                });
        }

        public virtual ICommit Commit(CommitAttempt attempt)
        {
            ICommit commit;
            try
            {
                commit = this.PersistCommit(attempt);
                Logger.Debug(Messages.CommitPersisted, attempt.CommitId);
            }
            catch (Exception e)
            {
                if (!(e is UniqueKeyViolationException))
                {
                    throw;
                }

                if (this.DetectDuplicate(attempt))
                {
                    Logger.Info(Messages.DuplicateCommit);
                    throw new DuplicateCommitException(e.Message, e);
                }

                Logger.Info(Messages.ConcurrentWriteDetected);
                throw new ConcurrencyException(e.Message, e);
            }
            return commit;
        }

        public virtual IEnumerable<ICommit> GetUndispatchedCommits()
        {
            Logger.Debug(Messages.GettingUndispatchedCommits);
            return
                this.ExecuteQuery(query => query.ExecutePagedQuery(this._dialect.GetUndispatchedCommits, (q, r) => { }))
                    .Select(x => x.GetCommit(this._serializer, this._dialect))
                    .ToArray(); // avoid paging
        }

        public virtual void MarkCommitAsDispatched(ICommit commit)
        {
            Logger.Debug(Messages.MarkingCommitAsDispatched, commit.CommitId);
            string streamId = this._streamIdHasher.GetHash(commit.StreamId);
            this.ExecuteCommand(cmd =>
                {
                    cmd.AddParameter(this._dialect.BucketId, commit.BucketId, DbType.AnsiString);
                    cmd.AddParameter(this._dialect.StreamId, streamId, DbType.AnsiString);
                    cmd.AddParameter(this._dialect.CommitSequence, commit.CommitSequence);
                    return cmd.ExecuteWithoutExceptions(this._dialect.MarkCommitAsDispatched);
                });
        }

        public virtual IEnumerable<IStreamHead> GetStreamsToSnapshot(string bucketId, int maxThreshold)
        {
            Logger.Debug(Messages.GettingStreamsToSnapshot);
            return this.ExecuteQuery(query =>
                {
                    string statement = this._dialect.GetStreamsRequiringSnapshots;
                    query.AddParameter(this._dialect.BucketId, bucketId, DbType.AnsiString);
                    query.AddParameter(this._dialect.Threshold, maxThreshold);
                    return
                        query.ExecutePagedQuery(statement,
                            (q, s) => q.SetParameter(this._dialect.StreamId, this._dialect.CoalesceParameterValue(s.StreamId()), DbType.AnsiString))
                            .Select(x => x.GetStreamToSnapshot());
                });
        }

        public virtual ISnapshot GetSnapshot(string bucketId, string streamId, int maxRevision)
        {
            Logger.Debug(Messages.GettingRevision, streamId, maxRevision);
            var streamIdHash = this._streamIdHasher.GetHash(streamId);
            return this.ExecuteQuery(query =>
                {
                    string statement = this._dialect.GetSnapshot;
                    query.AddParameter(this._dialect.BucketId, bucketId, DbType.AnsiString);
                    query.AddParameter(this._dialect.StreamId, streamIdHash, DbType.AnsiString);
                    query.AddParameter(this._dialect.StreamRevision, maxRevision);
                    return query.ExecuteWithQuery(statement).Select(x => x.GetSnapshot(this._serializer, streamId));
                }).FirstOrDefault();
        }

        public virtual bool AddSnapshot(ISnapshot snapshot)
        {
            Logger.Debug(Messages.AddingSnapshot, snapshot.StreamId, snapshot.StreamRevision);
            string streamId = this._streamIdHasher.GetHash(snapshot.StreamId);
            return this.ExecuteCommand((connection, cmd) =>
                {
                    cmd.AddParameter(this._dialect.BucketId, snapshot.BucketId, DbType.AnsiString);
                    cmd.AddParameter(this._dialect.StreamId, streamId, DbType.AnsiString);
                    cmd.AddParameter(this._dialect.StreamRevision, snapshot.StreamRevision);
                    this._dialect.AddPayloadParamater(this._connectionFactory, connection, cmd, this._serializer.Serialize(snapshot.Payload));
                    return cmd.ExecuteWithoutExceptions(this._dialect.AppendSnapshotToCommit);
                }) > 0;
        }

        public virtual void Purge()
        {
            Logger.Warn(Messages.PurgingStorage);
            foreach (var s in this._dialect.PurgeStorage.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
            {
                this.ExecuteCommand(cmd => cmd.ExecuteNonQuery(s.Trim()));
            }
        }

        public void Purge(string bucketId)
        {
            Logger.Warn(Messages.PurgingBucket, bucketId);
            this.ExecuteCommand(cmd =>
                {
                    cmd.AddParameter(this._dialect.BucketId, bucketId, DbType.AnsiString);
                    return cmd.ExecuteNonQuery(this._dialect.PurgeBucket);
                });
        }

        public void Drop()
        {
            Logger.Warn(Messages.DroppingTables);

            string[] tablesNames = this._dialect.Drop.Split(new[] { ";" }, StringSplitOptions.RemoveEmptyEntries);

            foreach (string name in tablesNames)
            {
                this.ExecuteCommand(cmd => cmd.ExecuteNonQuery(name.Trim() + ";"));
            }
        }

        public void DeleteStream(string bucketId, string streamId)
        {
            Logger.Warn(Messages.DeletingStream, streamId, bucketId);
            streamId = this._streamIdHasher.GetHash(streamId);
            this.ExecuteCommand(cmd =>
                {
                    cmd.AddParameter(this._dialect.BucketId, bucketId, DbType.AnsiString);
                    cmd.AddParameter(this._dialect.StreamId, streamId, DbType.AnsiString);
                    return cmd.ExecuteNonQuery(this._dialect.DeleteStream);
                });
        }

        public IEnumerable<ICommit> GetFrom(string bucketId, string checkpointToken)
        {
            LongCheckpoint checkpoint = LongCheckpoint.Parse(checkpointToken);
            Logger.Debug(Messages.GettingAllCommitsFromBucketAndCheckpoint, bucketId, checkpointToken);
            return this.ExecuteQuery(query =>
            {
                string statement = this._dialect.GetCommitsFromBucketAndCheckpoint;
                query.AddParameter(this._dialect.BucketId, bucketId, DbType.AnsiString);
                query.AddParameter(this._dialect.CheckpointNumber, checkpoint.LongValue);
                return query.ExecutePagedQuery(statement, (q, r) => { })
                    .Select(x => x.GetCommit(this._serializer, this._dialect));
            });
        }

        public IEnumerable<ICommit> GetFrom(string checkpointToken)
        {
            LongCheckpoint checkpoint = LongCheckpoint.Parse(checkpointToken);
            Logger.Debug(Messages.GettingAllCommitsFromCheckpoint, checkpointToken);
            return this.ExecuteQuery(query =>
            {
                string statement = this._dialect.GetCommitsFromCheckpoint;
                query.AddParameter(this._dialect.CheckpointNumber, checkpoint.LongValue);
                return query.ExecutePagedQuery(statement, (q, r) => { })
                    .Select(x => x.GetCommit(this._serializer, this._dialect));
            });
        }

        public bool IsDisposed
        {
            get { return this._disposed; }
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposing || this._disposed)
            {
                return;
            }

            Logger.Debug(Messages.ShuttingDownPersistence);
            this._disposed = true;
        }

        protected virtual void OnPersistCommit(IContribDbStatement cmd, CommitAttempt attempt)
        { }

        private ICommit PersistCommit(CommitAttempt attempt)
        {
            Logger.Debug(Messages.AttemptingToCommit, attempt.Events.Count, attempt.StreamId, attempt.CommitSequence, attempt.BucketId);
            string streamId = this._streamIdHasher.GetHash(attempt.StreamId);
            return this.ExecuteCommand((connection, cmd) =>
            {
                cmd.AddParameter(this._dialect.BucketId, attempt.BucketId, DbType.AnsiString);
                cmd.AddParameter(this._dialect.StreamId, streamId, DbType.AnsiString);
                cmd.AddParameter(this._dialect.StreamIdOriginal, attempt.StreamId);
                cmd.AddParameter(this._dialect.StreamRevision, attempt.StreamRevision);
                cmd.AddParameter(this._dialect.Items, attempt.Events.Count);
                cmd.AddParameter(this._dialect.CommitId, attempt.CommitId);
                cmd.AddParameter(this._dialect.CommitSequence, attempt.CommitSequence);
                cmd.AddParameter(this._dialect.CommitStamp, attempt.CommitStamp);
                cmd.AddParameter(this._dialect.Headers, this._serializer.Serialize(attempt.Headers));
                this._dialect.AddPayloadParamater(this._connectionFactory, connection, cmd, this._serializer.Serialize(attempt.Events.ToList()));
                this.OnPersistCommit(cmd, attempt);
                var checkpointNumber = cmd.ExecuteScalar(this._dialect.PersistCommit).ToLong();
                return new Commit(
                    attempt.BucketId,
                    attempt.StreamId,
                    attempt.StreamRevision,
                    attempt.CommitId,
                    attempt.CommitSequence,
                    attempt.CommitStamp,
                    checkpointNumber.ToString(CultureInfo.InvariantCulture),
                    attempt.Headers,
                    attempt.Events);
            });
        }

        private bool DetectDuplicate(CommitAttempt attempt)
        {
            string streamId = this._streamIdHasher.GetHash(attempt.StreamId);
            return this.ExecuteCommand(cmd =>
                {
                    cmd.AddParameter(this._dialect.BucketId, attempt.BucketId, DbType.AnsiString);
                    cmd.AddParameter(this._dialect.StreamId, streamId, DbType.AnsiString);
                    cmd.AddParameter(this._dialect.CommitId, attempt.CommitId);
                    cmd.AddParameter(this._dialect.CommitSequence, attempt.CommitSequence);
                    object value = cmd.ExecuteScalar(this._dialect.DuplicateCommit);
                    return (value is long ? (long)value : (int)value) > 0;
                });
        }

        protected virtual IEnumerable<T> ExecuteQuery<T>(Func<IContribDbStatement, IEnumerable<T>> query)
        {
            this.ThrowWhenDisposed();

            TransactionScope scope = this.OpenQueryScope();
            IDbConnection connection = null;
            IDbTransaction transaction = null;
            IContribDbStatement statement = null;

            try
            {
                connection = this._connectionFactory.Open();
                transaction = this._dialect.OpenTransaction(connection);
                statement = this._dialect.BuildStatement(scope, connection, transaction);
                statement.PageSize = this._pageSize;

                Logger.Verbose(Messages.ExecutingQuery);
                return query(statement);
            }
            catch (Exception e)
            {
                if (statement != null)
                {
                    statement.Dispose();
                }
                if (transaction != null)
                {
                    transaction.Dispose();
                }
                if (connection != null)
                {
                    connection.Dispose();
                }
                if (scope != null)
                {
                    scope.Dispose();
                }

                Logger.Debug(Messages.StorageThrewException, e.GetType());
                if (e is StorageUnavailableException)
                {
                    throw;
                }

                throw new StorageException(e.Message, e);
            }
        }

        protected virtual TransactionScope OpenQueryScope()
        {
            return this.OpenCommandScope() ?? new TransactionScope(TransactionScopeOption.Suppress);
        }

        private void ThrowWhenDisposed()
        {
            if (!this._disposed)
            {
                return;
            }

            Logger.Warn(Messages.AlreadyDisposed);
            throw new ObjectDisposedException(Messages.AlreadyDisposed);
        }

        private T ExecuteCommand<T>(Func<IContribDbStatement, T> command)
        {
            return this.ExecuteCommand((_, statement) => command(statement));
        }

        protected virtual T ExecuteCommand<T>(Func<IDbConnection, IContribDbStatement, T> command)
        {
            this.ThrowWhenDisposed();

            using (TransactionScope scope = this.OpenCommandScope())
            using (IDbConnection connection = this._connectionFactory.Open())
            using (IDbTransaction transaction = this._dialect.OpenTransaction(connection))
            using (IContribDbStatement statement = this._dialect.BuildStatement(scope, connection, transaction))
            {
                try
                {
                    Logger.Verbose(Messages.ExecutingCommand);
                    T rowsAffected = command(connection, statement);
                    Logger.Verbose(Messages.CommandExecuted, rowsAffected);

                    if (transaction != null)
                    {
                        transaction.Commit();
                    }

                    if (scope != null)
                    {
                        scope.Complete();
                    }

                    return rowsAffected;
                }
                catch (Exception e)
                {
                    Logger.Debug(Messages.StorageThrewException, e.GetType());
                    if (!RecoverableException(e))
                    {
                        throw new StorageException(e.Message, e);
                    }

                    Logger.Info(Messages.RecoverableExceptionCompletesScope);
                    if (scope != null)
                    {
                        scope.Complete();
                    }

                    throw;
                }
            }
        }

        protected virtual TransactionScope OpenCommandScope()
        {
            return new TransactionScope(this._scopeOption);
        }

        private static bool RecoverableException(Exception e)
        {
            return e is UniqueKeyViolationException || e is StorageUnavailableException;
        }

        private class StreamIdHasherValidator : IContribStreamIdHasher
        {
            private readonly IContribStreamIdHasher _streamIdHasher;
            private const int MaxStreamIdHashLength = 128;

            public StreamIdHasherValidator(IContribStreamIdHasher streamIdHasher)
            {
                if (streamIdHasher == null)
                {
                    throw new ArgumentNullException("streamIdHasher");
                }
                this._streamIdHasher = streamIdHasher;
            }
            public string GetHash(string streamId)
            {
                if (string.IsNullOrWhiteSpace(streamId))
                {
                    throw new ArgumentException(Messages.StreamIdIsNullEmptyOrWhiteSpace);
                }
                string streamIdHash = this._streamIdHasher.GetHash(streamId);
                if (string.IsNullOrWhiteSpace(streamIdHash))
                {
                    throw new InvalidOperationException(Messages.StreamIdHashIsNullEmptyOrWhiteSpace);
                }
                if (streamIdHash.Length > MaxStreamIdHashLength)
                {
                    throw new InvalidOperationException(Messages.StreamIdHashTooLong.FormatWith(streamId, streamIdHash, streamIdHash.Length, MaxStreamIdHashLength));
                }
                return streamIdHash;
            }
        }
    }
}
