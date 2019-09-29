using System;
using System.Collections.Concurrent;
using System.Data.SqlClient;
using Dapper;
using System.Threading;
using System.Data;
using MessageProcessorLib;
using NLog;

namespace MessageProcessorLib
{
    internal class MessageProcessor<TMessage, TId> : IDisposable where TMessage : IMessageModel<TId>
    {
        private static readonly ILogger logger = LogManager.GetCurrentClassLogger();

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
            logger.Info("Create message processor");
            logger.Trace(() => $"Create message processor with parameters: " +
            $"{nameof(connectionString)} = {connectionString}; " +
            $"{nameof(messagePool)} = {messagePool}; " +
            $"{nameof(getUpdateMessageQuery)} = {getUpdateMessageQuery}; " +
            $"{nameof(processMessage)} = {processMessage}; " +
            $"{nameof(isolationLevel)} = {isolationLevel}; " +
            $"{nameof(delayMessageProcess)} = {delayMessageProcess}; " +
            $"{nameof(delayMessageGet)} = {delayMessageGet};");

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
            logger.Trace("Start dispose processor");
            Stop();
            logger.Trace("End dispose processor");
        }

        public void Start()
        {
            logger.Trace("Start processor");

            _isStopped = false;
            var worker = new Thread(Process);
            worker.IsBackground = true;
            worker.Name = "Message_Processor";
            worker.Start();

            logger.Info("Start message processor");
            logger.Trace("End Start processor");
        }

        public void Stop()
        {
            logger.Info("Stop message processor");
            _isStopped = true;
        }

        private void Process()
        {
            logger.Trace(() => "Begin processing messages with connection: " + _connectionString);
            using (var connection = new SqlConnection(_connectionString))
            {
                TMessage message = default(TMessage);
                logger.Info(() => "Message processor Open Connection. Connection string: " + _connectionString);
                try
                {
                    connection.Open();
                    logger.Trace(() => $"Begin processing loop. Parameter {nameof(_isStopped)} = {_isStopped}.");
                    while (!_isStopped)
                    {
                        logger.Info("Message processor try get message from the queue.");
                        if (_messagePool.TryDequeue(out message))
                        {
                            logger.Info(() => "Process message: " + message.ToString());
                            _processMessage(message);

                            logger.Trace(() => $"Start processing transaction. Parameters: {nameof(_isolationLevel)} = {_isolationLevel}; {nameof(_isStopped)} = {_isStopped}");
                            using (var t = connection.BeginTransaction(_isolationLevel))
                            {
                                logger.Info("Adding results to the database. Result message: " + message.ToString());
                                connection.Execute(
                                    _getUpdateMessageQuery(message),
                                    transaction: t,
                                    param: new { id = message.Id });
                                t.Commit();
                            }
                            logger.Trace(() => $"Processor waiting for the next check iteration. Parameter {nameof(_isStopped)} = {_isStopped}.");
                            if (_isStopped)
                                Thread.Sleep(_delayMessageProcess);
                        }
                        else if (!_isStopped)
                        {
                            logger.Info(() => $"Processor can't get any messages from the queue. Wait {_delayMessageGet} ms");
                            Thread.Sleep(_delayMessageGet);
                        }
                    }
                }
                catch (Exception ex)
                {
                    logger.Fatal(ex, "Somthig going wrong when processed message: " + message?.ToString());
                    throw;
                }
            }
        }
    }
}
