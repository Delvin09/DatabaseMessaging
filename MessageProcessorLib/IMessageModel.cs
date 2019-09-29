using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MessageProcessorLib
{
    public interface IMessageModel<TId>
    {
        TId Id { get; }
        string Message { get; }
        DateTime CreatedDate { get; }
        DateTime FinishDate { get; }
    }
}
