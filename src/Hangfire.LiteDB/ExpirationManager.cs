﻿using System;
using System.Linq.Expressions;
using System.Threading;
using Hangfire.LiteDB.Entities;
using Hangfire.Logging;
using Hangfire.Server;
using LiteDB;

namespace Hangfire.LiteDB
{
    /// <summary>
    /// Represents Hangfire expiration manager for LiteDB database
    /// </summary>
    public class ExpirationManager : IBackgroundProcess, IServerComponent
    {
        private static readonly ILog Logger = LogProvider.For<ExpirationManager>();

        private readonly LiteDbStorage _storage;
        private readonly TimeSpan _checkInterval;

        /// <summary>
        /// Constructs expiration manager with one hour checking interval
        /// </summary>
        /// <param name="storage">LiteDb storage</param>
        public ExpirationManager(LiteDbStorage storage)
            : this(storage, TimeSpan.FromHours(1))
        {
        }

        /// <summary>
        /// Constructs expiration manager with specified checking interval
        /// </summary>
        /// <param name="storage">LiteDB storage</param>
        /// <param name="checkInterval">Checking interval</param>
        public ExpirationManager(LiteDbStorage storage, TimeSpan checkInterval)
        {
            _storage = storage ?? throw new ArgumentNullException(nameof(storage));
            _checkInterval = checkInterval;
        }

        /// <summary>
        /// Run expiration manager to remove outdated records
        /// </summary>
        /// <param name="context">Background processing context</param>
        public void Execute(BackgroundProcessContext context)
        {
            Execute(context.CancellationToken);
        }

        /// <summary>
        /// Run expiration manager to remove outdated records
        /// </summary>
        /// <param name="cancellationToken">Cancellation token</param>
        public void Execute(CancellationToken cancellationToken)
        {
            using (HangfireDbContext connection = _storage.CreateAndOpenConnection())
            {
                DateTime now = DateTime.UtcNow;

                RemoveExpiredRecord(connection.Job, _ => _.ExpireAt, now);
                RemoveExpiredRecord(connection.StateData.OfType<LiteExpiringKeyValue>(), _ => _.ExpireAt, now);
            }

            cancellationToken.WaitHandle.WaitOne(_checkInterval);
        }

        /// <summary>
        /// Returns text representation of the object
        /// </summary>
        public override string ToString()
        {
            return "LiteDB Expiration Manager";
        }

        private static void RemoveExpiredRecord<TEntity, TField>(LiteCollection<TEntity> collection, Expression<Func<TEntity, TField>> expression, TField now)
        {
            Logger.DebugFormat("Removing outdated records from table '{0}'...", collection.Name);

            collection.Delete(Query.LT(expression, now));
        }
    }
}