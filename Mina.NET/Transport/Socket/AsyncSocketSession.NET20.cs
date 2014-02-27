﻿using System;
using System.Net.Sockets;
using Mina.Core.Buffer;
using Mina.Core.Service;
using Mina.Util;

namespace Mina.Transport.Socket
{
    class AsyncSocketSession : SocketSession
    {
        private readonly Byte[] _readBuffer;

        public AsyncSocketSession(IoService service, IoProcessor<SocketSession> processor, System.Net.Sockets.Socket socket)
            : base(service, processor, socket)
        {
            _readBuffer = new Byte[service.SessionConfig.ReadBufferSize];
        }

        protected override void BeginReceive()
        {
            Socket.BeginReceive(_readBuffer, 0, _readBuffer.Length, SocketFlags.None, ReceiveCallback, Socket);
        }

        private void ReceiveCallback(IAsyncResult ar)
        {
            System.Net.Sockets.Socket socket = (System.Net.Sockets.Socket)ar.AsyncState;
            Int32 read = 0;
            try
            {
                read = socket.EndReceive(ar);
            }
            catch (Exception ex)
            {
                ExceptionMonitor.Instance.ExceptionCaught(ex);
            }

            if (read > 0)
            {
                EndReceive(ByteBufferAllocator.Instance.Wrap(_readBuffer, 0, read));
                return;
            }

            // closed
            Processor.Remove(this);
        }

        protected override void BeginSend(IoBuffer buf)
        {
            ArraySegment<Byte> array = buf.GetRemaining();
            try
            {
                Socket.BeginSend(array.Array, array.Offset, array.Count, SocketFlags.None, SendCallback, new SendingContext(Socket, buf));
            }
            catch (ObjectDisposedException)
            {
                // ignore
            }
            catch (Exception ex)
            {
                ExceptionMonitor.Instance.ExceptionCaught(ex);
            }
        }

        private void SendCallback(IAsyncResult ar)
        {
            SendingContext sc = (SendingContext)ar.AsyncState;
            Int32 written;
            try
            {
                written = sc.socket.EndSend(ar);
            }
            catch (Exception ex)
            {
                ExceptionMonitor.Instance.ExceptionCaught(ex);

                // closed
                Processor.Remove(this);

                return;
            }

            sc.buffer.Position += written;
            EndSend(written);
        }

        class SendingContext
        {
            public readonly System.Net.Sockets.Socket socket;
            public readonly IoBuffer buffer;

            public SendingContext(System.Net.Sockets.Socket s, IoBuffer b)
            {
                socket = s;
                buffer = b;
            }
        }
    }
}
