using MessageProcessorLib;
using NLog;
using NLog.Windows.Forms;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace WinFormClient
{
    public class MessageModel : IMessageModel<int>
    {
        public int Id { get; set; }
        public string Message { get; set; }
        public DateTime CreatedDate { get; set; }
        public DateTime FinishDate { get; set; }
    }

    public partial class Form1 : Form
    {
        const string ConnectionString = @"Data Source=(LocalDb)\MSSQLLocalDB;Integrated Security=True";
        private static readonly ILogger logger = LogManager.GetCurrentClassLogger();
        private readonly object processingLock = new object();

        public Form1()
        {
            InitializeComponent();
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            RichTextBoxTarget target = new RichTextBoxTarget();
            target.Layout = "[${date:format=HH\\:MM\\:ss}] [${logger}]> ${message}";
            target.ControlName = "richTextBox1";
            target.FormName = "Form1";
            target.UseDefaultRowColoringRules = true;

            NLog.Config.SimpleConfigurator.ConfigureForTargetLogging(target, LogLevel.Trace);

            //Logger logger = LogManager.GetLogger("Example");
            //logger.Trace("trace log message");
            //            logger.Debug("debug log message");
            //            logger.Info("info log message");
            //            logger.Warn("warn log message");
            //            logger.Error("error log message");
            //            logger.Fatal("fatal log message");

            logger.Trace(() => "Start method " + nameof(Form1_Load));
            logger.Info(() => "Start Message provider.");

            var messageProvider = new MessageProvider<MessageModel, int>(ConnectionString, System.Data.IsolationLevel.ReadCommitted);

            logger.Info(() => "Start message watcher.");
            messageProvider.StartWatcher(() => @"select * from [Messages] where FinishDate is null order by CreatedDate",
                (int)TimeSpan.FromMinutes(1).TotalMilliseconds);
            logger.Info(() => "Start message processor.");
            messageProvider.StartProcessor(
                message => "UPDATE Messages SET FinishDate = GETDATE() where Id = @id",
                message => DoS(),
                (int)TimeSpan.FromSeconds(30).TotalMilliseconds,
                (int)TimeSpan.FromSeconds(10).TotalMilliseconds);
        }

        private void button1_Click(object sender, EventArgs e)
        {
            Task.Factory.StartNew(DoS);
        }

        private void DoS()
        {
            lock (processingLock)
            {
                button1.Invoke(new Action(() => button1.Enabled = false));
                try
                {
                    Thread.Sleep(1000 * 30);
                }
                finally
                {
                    button1.Invoke(new Action(() => button1.Enabled = true));
                }
            }
        }
    }
}
