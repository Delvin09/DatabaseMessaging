using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using System.Data;

namespace MessageProcessorLib
{
    public class MessageProvider<TMessage, TId> : IDisposable where TMessage : IMessageModel<TId>
    {
        private readonly ConcurrentQueue<TMessage> _messagePool;
        private readonly string _connectionString;
        private readonly IsolationLevel _isolationLevel;
        private MessageWatcher<TMessage, TId> _messageWatcher;
        private MessageProcessor<TMessage, TId> _messageProcessor;

        public MessageProvider(string connectionString,
            IsolationLevel isolationLevel = IsolationLevel.Unspecified)
        {
            _connectionString = connectionString;
            _isolationLevel = isolationLevel;
            _messagePool = new ConcurrentQueue<TMessage>();
        }

        public void Dispose()
        {
            _messageWatcher?.Dispose();
            _messageWatcher = null;
            _messageProcessor?.Dispose();
            _messageProcessor = null;
        }

        public void StartWatcher(Func<string> getMessageQuery, int delay = -1)
        {
            _messageWatcher = delay >= 0
                ? new MessageWatcher<TMessage, TId>(_connectionString, _messagePool,
                    getMessageQuery, _isolationLevel, delay)
                : new MessageWatcher<TMessage, TId>(_connectionString, _messagePool,
                    getMessageQuery, _isolationLevel);

            _messageWatcher.Start();
        }

        public void StartProcessor(Func<TMessage, string> getUpdateMessageQuery,
            Action<TMessage> processMessage, int delayMessageGet = -1, int delayMessageProcess = -1)
        {
            if (delayMessageGet < 0 && delayMessageProcess < 0)
                _messageProcessor = new MessageProcessor<TMessage, TId>(_connectionString, _messagePool,
                    getUpdateMessageQuery, processMessage, _isolationLevel);
            else if (delayMessageGet >= 0 && delayMessageProcess < 0)
                _messageProcessor = new MessageProcessor<TMessage, TId>(_connectionString, _messagePool,
                    getUpdateMessageQuery, processMessage, _isolationLevel, delayMessageGet: delayMessageGet);
            else if (delayMessageGet < 0 && delayMessageProcess >= 0)
                _messageProcessor = new MessageProcessor<TMessage, TId>(_connectionString, _messagePool,
                    getUpdateMessageQuery, processMessage, _isolationLevel, delayMessageProcess: delayMessageProcess);
            else
                _messageProcessor = new MessageProcessor<TMessage, TId>(_connectionString, _messagePool,
                    getUpdateMessageQuery, processMessage, _isolationLevel,
                    delayMessageProcess: delayMessageProcess,
                    delayMessageGet: delayMessageGet);

            _messageProcessor.Start();
        }
    }
}
