using System;
using System.Data.SqlClient;
using System.Threading;

namespace DatabaseMessaging
{
    class Program
    {
        const string ConnectionString = @"Data Source=(LocalDb)\MSSQLLocalDB;Integrated Security=True";
        static Random random = new Random();
        static void Main(string[] args)
        {
            using (var connection = new SqlConnection(ConnectionString))
            {
                connection.Open();
                while (true)
                {
                    using (var tran = connection.BeginTransaction())
                    {
                        using (var cmd = connection.CreateCommand())
                        {
                            cmd.Transaction = tran;
                            cmd.Connection = connection;
                            cmd.CommandText = @"Insert Messages([Message]) VALUES (N'Message_Test')";
                            var result = cmd.ExecuteNonQuery();
                            Console.WriteLine("Message added with code: " + result);
                            tran.Commit();
                        }
                    }
                    Thread.Sleep(random.Next(1000 * 10, 1000 * 20));
                }
            }
        }
    }
}
