/*
Copyright (c) 2021, Stefan Berndt

All rights reserved.

Redistribution and use in source and binary forms, with or without modification, are permitted provided that the following conditions are met:
1. Redistributions of source code must retain the above copyright notice, this list of conditions and the following disclaimer.
2. Redistributions in binary form must reproduce the above copyright notice, this list of conditions and the following disclaimer in the documentation and/or 
   other materials provided with the distribution.

THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, 
BUT NOT LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE DISCLAIMED. 
IN NO EVENT SHALL THE COPYRIGHT HOLDER OR CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES 
(INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) 
HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE) 
ARISING IN ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
*/

using AfwDynamicGraphics;
using AfwExpressionHandling;
using System;
using System.Net.Sockets;
using System.Text;
using System.Windows;


namespace PG2NetworkHelper
{
    [PrimitiveItemAttribute("{29934942-E015-4DAA-8EF9-3763F7839F85}", "Custom:UdpSender", "UdpSender", "Custom Controls", "Sends UDP Data packets")]
    class PG2UdpSender : FrameItem
    {
        private string host = "127.0.0.1";
        private long port = 8008;
        private bool sendTrigger = false;
        private bool sendTriggerW = false;
        private string sendData = "";
        private long encoding = 2;
        private static UdpClient client = new UdpClient();
        private static Encoding[] encoders;

        // enums
        private enum pEncodingType { BINARY, ASCII, UTF8, UTF16LE, UTF16BE, UTF32, Systemdefault }

        // my properties
        private static readonly PropertyDesc[] myprops = new PropertyDesc[]
        {
            new PropertyDesc("Hostname", StringType.Singleton, 100, "Name or IP of the remote host, or broadcast IP", "Connection"),
            new PropertyDesc("Hostport", IntegerType.Singleton, 101, "Port number of the remote host", "Connection"),
            new PropertyDesc("SendTrigger", BooleanType.Singleton, 102, "Send data on false->true.", "Connection"),
            new PropertyDesc("SendData", StringType.Singleton, 103, "Text Data to send", "Connection"),
            new PropertyDesc("Encoding", new Enumeration(typeof(pEncodingType),false), 104, "How to encode text data.", "Connection")
        };

        // all properties
        private static readonly PropertyDesc[] allprops = new PropertyDesc[FrameItem.FiGetNumberOfProps(false, false, false, false, false) + myprops.Length];

        protected override PropertyDesc[] GetPropertyDescriptions()
        {
            return allprops;
        }

        // constructors
        static PG2UdpSender()
        {
            encoders = new Encoding[] { Encoding.Unicode, Encoding.ASCII, Encoding.UTF8, Encoding.Unicode, Encoding.BigEndianUnicode, Encoding.UTF32, Encoding.Default };
            int n = 0;
            FrameItem.FiFillInPropertyDescriptions(allprops, ref n, false, false, false, false, false);
            foreach (PropertyDesc desc in myprops)
            {
                allprops[n++] = desc;
            }
        }

        public PG2UdpSender() : base(false, false, false)  {}

        public PG2UdpSender(PG2UdpSender other, GraphicItemVisual otherVisual) : base(other, otherVisual)
        {
            if (other != null)
            {
                host = other.host;
                port = other.port;
                sendTrigger = other.sendTrigger;
                sendData = other.sendData;
                encoding = other.encoding;
            }
        }

        public override GraphicItem GetRunTimeInstance(IElementView elementView, GraphicItemVisual visual)
        {
            return new PG2UdpSender(this, visual);
        }

        // required but useless
        protected override System.Windows.Size GetDefaultSize()
        {
            return new Size(100.0, 100.0);
        }

        // required but useless
        protected override void InitVisual(IElementView elementView, ulong noValueEffects)
        {

        }

        // required but useless
        protected override void UpdateVisual(IElementView elementView, ulong updateReason, ulong noValueEffects)
        {

        }

        // called if property is changed
        protected override void TransferValue(IDataAccess accessor, int accessIndex, int propertyIndex, bool writeOperation)
        {
            if (accessor == null) return;
            switch (propertyIndex)
            {
                case 100:    // property "Hostname"
                    accessor.TransferString(writeOperation, accessIndex, ref host);
                    break;
                case 101:    // property "Hostport"
                    accessor.TransferInteger(writeOperation, accessIndex, ref port);
                    break;
                case 102:    // property "sendTrigger"
                    accessor.TransferBoolean(writeOperation, accessIndex, ref sendTrigger);
                    if (sendTrigger && !sendTriggerW)
                    {
                        byte[] b = encoders[encoding].GetBytes(sendData); // encode text
                        if (encoding == 0)
                        {
                            byte[] u = new byte[b.Length / 2];
                            for (int i = 0; i < u.Length; i++)
                            {
                                u[i] = b[i * 2];
                            }
                            b = u;
                        }
                        client.SendAsync(b, b.Length, host, (int)port);  // send text
                    }
                    sendTriggerW = sendTrigger;
                    break;
                case 103:    // property "sendData"
                    accessor.TransferString(writeOperation, accessIndex, ref sendData);
                    break;
                case 104:    // property "encoding"
                    accessor.TransferInteger(writeOperation, accessIndex, ref encoding);
                    break;
                default:    // anything inherited
                    base.TransferValue(accessor, accessIndex, propertyIndex, writeOperation);
                    break;
            }
        }

    }
}
