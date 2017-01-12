using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Apache.NMS;
using System.Collections;
/**
 * 类名: MomApi
 * 功能：封装了对消息中间件的发送和接收消息的接口
 * Author: Rowling
 * Time: 2017/1/10
 * version: 0.0  初始创建
 */

namespace MomApi
{
    //同步消息的接收模式
    public enum MsgReceiveMode
    {
        WaitOneMsg, //直到接收到一条信息后才会返回
        WaitFixTime, //指定最长的等待时间，如果未到达指定时间就收到了消息立即返回，到达指定时间后如果没收到任何消息返回null
        NoWait       //不管是否能够收到消息都立即返回，未收到消息则返回null
    };

    public interface IMomApi : IDisposable
    {
        void sendToDes(String destination, String content, bool bQueue);
        bool AddOrDelListenerToDes(bool bQueue, String destination, MessageListener msgListener, bool bAdd);
        String syncReceiveFromDes(bool bQueue, String destination, MsgReceiveMode receiveMode, TimeSpan waitTime);
    }

    public class MomApi:IMomApi
    {
        private static MomApi momApi = null;
        private Uri connecturi = new Uri("activemq:tcp://120.92.201.197:61616");
        private IConnectionFactory factory = null;
        private IConnection connection = null;
        private ISession session = null;
        private Hashtable nameToQueDes = null;
        private Hashtable nameToTopicDes = null;
        private Hashtable desToConsumers = null;
        private Hashtable desToProducers = null;
        private MomApi()
        {
            factory = new NMSConnectionFactory(connecturi);
            connection = factory.CreateConnection();
            session = connection.CreateSession();
            nameToQueDes = new Hashtable();
            nameToTopicDes = new Hashtable();
            desToConsumers = new Hashtable();
            desToProducers = new Hashtable();
            connection.Start();
        }

        /**
         * 方法名：getMomApi
         * 功能：  获取MomApi的实例
         * 返回值: 已创建的MomApi实例
         */
        public static IMomApi getIMomApi()
        {
            if(null == momApi)
            {
                momApi = new MomApi();
            }
            return momApi;
        }
        /**
         * 方法名：sendToDes
         * 功能：  将一条消息发送到指定的queue或topic
         * 参数：
         *      bQueue:  true表示destination是queue，false表示destination是topic
         *      destination: queue或topic的名称
         *      content:     待发送的本条信息的内容
         * 返回值：void
         */
        public void sendToDes(String destination, String content, bool bQueue)
        {
            if (String.IsNullOrEmpty(destination))
            {
                throw new ApplicationException("destination can't be null");
            }
            if (String.IsNullOrEmpty(content))
            {
                throw new ApplicationException("content can't be null");
            }

            IDestination iDes = this.getDestination(destination, bQueue);
            if(null == iDes)
            {
                throw new ApplicationException(String.Format("获取'{0}'对应的destination失败", destination));
            }
            IMessageProducer iProducer = this.getDesProducer(iDes);
            if(null != iProducer)
            {
                ITextMessage msgSend = session.CreateTextMessage(content);
                iProducer.Send(msgSend);
            }
            
        }

        /**
        * 方法名：AddOrDelListenerToDes
        * 功能：  为某一指定的队列，添加或删除一个MessageListener，以便异步接收消息
        * 参数：
        *      destination: queue的名称
        *      msgListener: 添加或删除的MessageListener
        *      bAdd:        值为true,添加一个MessageListener
        *                   值为false,移除一个MessageListener
        * 返回值：添加或删除操作是否成功
        */
        public bool AddOrDelListenerToDes(bool bQueue, String destination, MessageListener msgListener, bool bAdd)
        {
            bool bSucess = false;
            if (String.IsNullOrEmpty(destination))
            {
                throw new ApplicationException("destination can't be null");
            }
            IDestination iDes = this.getDestination(destination, bQueue);
            if (null == iDes)
            {
                throw new ApplicationException(String.Format("获取'{0}'对应的destination失败", destination));
            }
            IMessageConsumer iConsumer = this.getDesConsumer(iDes);
            if (null != iConsumer)
            {
                if (bAdd)
                    iConsumer.Listener += msgListener;
                else
                    iConsumer.Listener -= msgListener;
                bSucess = true;
            }
            return bSucess;
        }

        /**
         * 方法名：syncReceiveFromDes
         * 功能：从指定的queue同步接收消息
         * 参数：
         *          destination: queue的名称
         *          receiveMode: 消息的接收模式
         *          waitTime:  当消息的接收模式为MsgReceiveMode.WaitFixTime时指定等待的时间
         * 返回值：如果收到了一条消息则返回消息的内容
         *         如果未收到消息则返回null
         */
        public String syncReceiveFromDes(bool bQueue, String destination, MsgReceiveMode receiveMode, TimeSpan waitTime)
        {
            if (String.IsNullOrEmpty(destination))
            {
                throw new ApplicationException("destination can't be null");
            }
            IDestination iDes = this.getDestination(destination, bQueue);
            if (null == iDes)
            {
                throw new ApplicationException(String.Format("获取'{0}'对应的destination失败", destination));
            }
            IMessageConsumer iConsumer = this.getDesConsumer(iDes);        
            if(null == iConsumer)
            {
                return null;
            }
            ITextMessage recMsg = null;

            switch (receiveMode)
            {
                case MsgReceiveMode.WaitOneMsg:
                    {
                        recMsg = iConsumer.Receive() as ITextMessage;
                    }
                    break;
                case MsgReceiveMode.WaitFixTime:
                    {
                        if (null == waitTime)
                            throw new ApplicationException("必须传入有效的waitTime");
                        recMsg = iConsumer.Receive(waitTime) as ITextMessage;
                    }
                    break;
                case MsgReceiveMode.NoWait:
                    {
                        recMsg = iConsumer.ReceiveNoWait() as ITextMessage;
                    }
                    break;
            }

            if (null == recMsg)
                return null;
            else
                return recMsg.Text;
        }

        /**
        * 方法名：Dispose
        * 功能：释放资源
        */
        void IDisposable.Dispose()
        {
            if(session != null)
            {
                session.Dispose();
                session.Close();
                session = null;
            }
            if(connection != null)
            {
                connection.Stop();
                connection.Dispose();
                connection.Close();
                connection = null;
            }
           // throw new NotImplementedException();
        }

        /**
        * 方法名：getQueDes
        * 功能：获取name对应的IDestination
        * 参数：
        *       name:  queue或topic的名字
        *       bQueue: true表示queue, false表示topic
        * 返回值：name对应的IDestination
        */
        private IDestination getDestination(String name, bool bQueue)
        {
            IDestination iRt = null;
            name.Trim();
            if (bQueue)
            {
                if (nameToQueDes.Contains(name))
                {
                    iRt = nameToQueDes[name] as IDestination;
                }
                else
                {
                    String strTemp = "queue://" + name;
                    iRt = session.GetDestination(strTemp);
                    nameToQueDes.Add(name, iRt);
                }
            }
            else
            {
                if (nameToTopicDes.Contains(name))
                {
                    iRt = nameToTopicDes[name] as IDestination;
                }
                else
                {
                    String strTemp = "topic://" + name;
                    iRt = session.GetDestination(strTemp);
                    nameToTopicDes.Add(name, iRt);
                }
            }
            return iRt;
        }

        /**
        * 方法名：getDesProducer
        * 功能：  获取某一IDestination对应的IMessageProducer
        * 参数：
        *         destination: IDestination类型的对象引用
        * 返回值：该IDestination对应的IMessageProducer
        */
        private IMessageProducer getDesProducer(IDestination destination)
        {
            IMessageProducer iProducer = null;
            if (desToProducers.Contains(destination))
            {
                iProducer = desToProducers[destination] as IMessageProducer;
            }
            else
            {
                iProducer = session.CreateProducer(destination);
                desToProducers.Add(destination, iProducer);
            }
            return iProducer;
        }

        /**
        * 方法名：getDesConsumer
        * 功能：  获取某一IDestination对应的IMessageConsumer
        * 参数：
        *         destination: IDestination类型的对象引用
        * 返回值：
        *         该IDestination对应的IMessageConsumer实例
        */
        private IMessageConsumer getDesConsumer(IDestination destination)
        {
            IMessageConsumer iConsumer = null;
            if (desToConsumers.Contains(destination))
            {
                iConsumer = desToConsumers[destination] as IMessageConsumer;
            }
            else
            {
                iConsumer = session.CreateConsumer(destination);
                desToConsumers.Add(destination, iConsumer);
            }
            return iConsumer;
        }      
    }  
}
