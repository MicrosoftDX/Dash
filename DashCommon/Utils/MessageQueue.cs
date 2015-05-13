using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Dash.Common.Utils
{
    public interface IMessage
    {
        T GetPayload<T>();
    }
    public interface IMessageQueue
    {
        public void Enqueue(IMessage message);
        public IMessage Dequeue();
        public IMessage Peek();
    }
    class MessageQueue
    {
    }
}
