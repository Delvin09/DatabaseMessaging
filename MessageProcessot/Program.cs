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
        const string ConnectionString = @"Data Source=(LocalDb)\MSSQLLocalDB;Integrated Security=True";
        static void Main(string[] args)
        {
            var messageProvider = new MessageProvider<MessageModel, int>(ConnectionString, System.Data.IsolationLevel.ReadCommitted);
            messageProvider.StartWatcher(() => @"select * from [Messages] where FinishDate is null order by CreatedDate",
                (int)TimeSpan.FromMinutes(1).TotalMilliseconds);
            messageProvider.StartProcessor(
                message => "UPDATE Messages SET FinishDate = GETDATE() where Id = @id",
                message => Console.WriteLine("Message Process: {0}", message.Id),
                (int)TimeSpan.FromSeconds(30).TotalMilliseconds,
                (int)TimeSpan.FromSeconds(10).TotalMilliseconds);

            Console.ReadLine();

            messageProvider.Dispose();
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
