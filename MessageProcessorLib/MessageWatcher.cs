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

namespace MessageProcessorLib
{
    internal class MessageWatcher<TMessage, TId> : IDisposable where TMessage : IMessageModel<TId>
    {
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
            _connectionString = connectionString;
            _messagePool = messagePool;
            _isolationLevel = isolationLevel;
            _delay = delay;
            _getMessageQuery = getMessageQuery;
        }

        public void Dispose()
        {
            Stop();
        }

        public void Start()
        {
            _isStopped = false;
            var worker = new Thread(Watch);
            worker.IsBackground = true;
            worker.Name = "Database_Watcher";
            worker.Start();
        }

        public void Stop()
        {
            _isStopped = true;
        }

        private void Watch()
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                connection.Open();
                while (!_isStopped)
                {
                    using (var t = connection.BeginTransaction(_isolationLevel))
                    {
                        var messages = connection.Query<TMessage>(_getMessageQuery(),
                            transaction: t);
                        foreach (var message in messages)
                            _messagePool.Enqueue(message);
                    }

                    if (!_isStopped)
                        Thread.Sleep(_delay);
                }
            }
        }
    }
}
