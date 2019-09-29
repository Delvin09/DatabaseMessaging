using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using NLog;

namespace MessageProcessorLib
{
    internal class MessageWatcher<TMessage, TId> : IDisposable where TMessage : IMessageModel<TId>
    {
        private static readonly ILogger logger = LogManager.GetCurrentClassLogger();

        private readonly string _connectionString;
        private readonly ConcurrentQueue<TMessage> _messagePool;
        private readonly IsolationLevel _isolationLevel;
        private readonly int _delay;
        private readonly Func<string> _getMessageQuery;
        private volatile bool _isStopped = false;

        public MessageWatcher(string connectionString,
            ConcurrentQueue<TMessage> messagePool,
            Func<string> getMessageQuery,
            IsolationLevel isolationLevel = IsolationLevel.Unspecified,
            int delay = 1000 * 60)
        {
            logger.Info("Create watcher");
            logger.Trace(() => $"Create watcher with parameters: {nameof(connectionString)} = {connectionString}; " +
                $"{nameof(messagePool)} = {messagePool}; {nameof(getMessageQuery)} + {getMessageQuery}; " +
                $"{nameof(isolationLevel)} = {isolationLevel}; {nameof(delay)} = {delay}");

            _connectionString = connectionString;
            _messagePool = messagePool;
            _isolationLevel = isolationLevel;
            _delay = delay;
            _getMessageQuery = getMessageQuery;
        }

        public void Dispose()
        {
            logger.Trace("Start Dispose watcher");
            Stop();
            logger.Trace("End Dispose watcher");
        }

        public void Start()
        {
            logger.Trace("Start Watcher");

            _isStopped = false;
            var worker = new Thread(Watch);
            worker.IsBackground = true;
            worker.Name = "Database_Watcher";
            worker.Start();

            logger.Info("Start message watcher");
            logger.Trace("End Start Watcher");
        }

        public void Stop()
        {
            _isStopped = true;
            logger.Info("Watcher is stopped");
        }

        private void Watch()
        {
            logger.Trace(() => "Begin watch with connection: " + _connectionString);
            using (var connection = new SqlConnection(_connectionString))
            {
                logger.Info(() => "Message watcher Open Connection. Connection string: " + _connectionString);
                try
                {
                    connection.Open();
                    logger.Trace(() => $"Begin watching loop. Parameter {nameof(_isStopped)} = {_isStopped}.");
                    while (!_isStopped)
                    {
                        logger.Trace(() => $"Start watching transaction. Parameters: {nameof(_isolationLevel)} = {_isolationLevel}; {nameof(_isStopped)} = {_isStopped}");
                        using (var t = connection.BeginTransaction(_isolationLevel))
                        {
                            logger.Info("Get messages from database.");
                            var messages = connection.Query<TMessage>(_getMessageQuery(),
                                transaction: t);
                            logger.Info("Put messages to the queue.");
                            foreach (var message in messages)
                                _messagePool.Enqueue(message);
                            logger.Info(() => $"Wait for the next check. Delay: {_delay} ms");
                        }

                        logger.Trace(() => $"Watcher waiting for the next check iteration. Parameter {nameof(_isStopped)} = {_isStopped}.");
                        if (!_isStopped)
                            Thread.Sleep(_delay);
                    }
                }
                catch (Exception ex)
                {
                    logger.Fatal(ex, "Somthing going wrong when wathing messages.");
                    throw;
                }
            }
            logger.Trace("End watch");
        }
    }
}
