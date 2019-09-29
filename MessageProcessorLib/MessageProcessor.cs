using System;
using System.Collections.Concurrent;
using System.Data.SqlClient;
using Dapper;
using System.Threading;
using System.Data;
using MessageProcessorLib;

namespace MessageProcessorLib
{
    internal class MessageProcessor<TMessage, TId> : IDisposable where TMessage : IMessageModel<TId>
    {
        private readonly string _connectionString;
        private readonly ConcurrentQueue<TMessage> _messagePool;
        private readonly Func<TMessage, string> _getUpdateMessageQuery;
        private readonly Action<TMessage> _processMessage;
        private readonly IsolationLevel _isolationLevel;
        private readonly int _delayMessageProcess;
        private readonly int _delayMessageGet;
        private volatile bool _isStopped = false;

        public MessageProcessor(string connectionString, ConcurrentQueue<TMessage> messagePool,
            Func<TMessage, string> getUpdateMessageQuery,
            Action<TMessage> processMessage,
            IsolationLevel isolationLevel = IsolationLevel.Unspecified,
            int delayMessageProcess = 1000 * 60,
            int delayMessageGet = 1000 * 60)
        {
            _connectionString = connectionString;
            _messagePool = messagePool;
            _getUpdateMessageQuery = getUpdateMessageQuery;
            _processMessage = processMessage;
            _isolationLevel = isolationLevel;
            _delayMessageProcess = delayMessageProcess;
            _delayMessageGet = delayMessageGet;
        }

        public void Dispose()
        {
            Stop();
        }

        public void Start()
        {
            _isStopped = false;
            var worker = new Thread(Process);
            worker.IsBackground = true;
            worker.Name = "Message_Processor";
            worker.Start();
        }

        public void Stop()
        {
            _isStopped = true;
        }

        private void Process()
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                connection.Open();
                while (!_isStopped)
                {
                    if (_messagePool.TryDequeue(out TMessage message))
                    {
                        _processMessage(message);

                        using (var t = connection.BeginTransaction(_isolationLevel))
                        {
                            connection.Execute(
                                _getUpdateMessageQuery(message),
                                //,
                                transaction: t,
                                param: new { id = message.Id });
                            t.Commit();
                        }
                        if (_isStopped)
                            Thread.Sleep(_delayMessageProcess);
                    }
                    else if (!_isStopped)
                        Thread.Sleep(_delayMessageGet);
                }
            }
        }
    }
}
