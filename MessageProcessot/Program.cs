using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MessageProcessorLib;
using NLog;

namespace MessageProcessot
{
    class Program
    {
        static readonly ILogger logger = LogManager.GetCurrentClassLogger();
        const string ConnectionString = @"Data Source=(LocalDb)\MSSQLLocalDB;Integrated Security=True";
        static void Main(string[] args)
        {
            logger.Trace(() => "Start method " + nameof(Main));
            logger.Info(() => "Start Message provider.");

            var messageProvider = new MessageProvider<MessageModel, int>(ConnectionString, System.Data.IsolationLevel.ReadCommitted);

            logger.Info(() => "Start message watcher.");
            messageProvider.StartWatcher(() => @"select * from [Messages] where FinishDate is null order by CreatedDate",
                (int)TimeSpan.FromMinutes(1).TotalMilliseconds);
            logger.Info(() => "Start message processor.");
            messageProvider.StartProcessor(
                message => "UPDATE Messages SET FinishDate = GETDATE() where Id = @id",
                message => Console.WriteLine("Message Process: {0}", message.Id),
                (int)TimeSpan.FromSeconds(30).TotalMilliseconds,
                (int)TimeSpan.FromSeconds(10).TotalMilliseconds);

            logger.Info(() => "Wait for end.");
            Console.ReadLine();

            logger.Info(() => "Stop provider.");
            messageProvider.Dispose();

            logger.Trace("End method " + nameof(Main));
            Console.ReadLine();
        }
    }

    

    public class MessageModel : IMessageModel<int>
    {
        public int Id { get; set; }
        public string Message { get; set; }
        public DateTime CreatedDate { get; set; }
        public DateTime FinishDate { get; set; }
    }
}
