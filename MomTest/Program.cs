using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MomApi;
using Apache.NMS;
namespace MomTest
{
    class Program
    {
        public static void Main(string[] args)
        {
            using (MomApi.IMomApi momApi = MomApi.MomApi.getIMomApi())
            {

                String strQueue = "test.queue";
                bool bQueue = true;
                momApi.sendToDes(strQueue, "12345", bQueue);
                /*  //同步接收
                String strReceive = momApi.syncReceiveFromQue(strQueue, MsgReceiveMode.WaitOneMsg, TimeSpan.Zero);
                if(null == strReceive)
                {
                    Console.WriteLine("Can't receive any message");
                }
                else
                {
                    Console.WriteLine(strReceive);
                }
                */

                //异步接收
                momApi.AddOrDelListenerToDes(bQueue, strQueue, Program.OnMessage, true);
            }
        }
        //测试用
        protected static void OnMessage(IMessage receivedMsg)
        {
            ITextMessage message = receivedMsg as ITextMessage;
            if (null != message.Text)
                Console.WriteLine(message.Text);
        }
    }
}
