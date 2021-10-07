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
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;

namespace PG2NetworkHelper
{
    [PrimitiveItemAttribute("{3917A2F9-FB15-4A1F-8C79-29E9E4FD5FE0}", "Custom:TcpClient", "TcpClient", "Custom Controls", "Transfer Data from/to a TCP server")]
    public class PG2TcpClient : FrameItem, ITimerUpdateable, IDisposable
    {
        // instance variables
        private string host = "127.0.0.1";
        private long port = 80;
        private bool connect = false;
        private bool sendTrigger = false;
        private bool sendTriggerW = false;
        private string sendData = "";
        private long encoding = 2;
        private PropertyRefValue responseData = null;
        private byte[] responseText = new byte[0];
        private long responseTimeout = 100;
        private long responseTimer = 0;
        private long connectTimer = 0;
        private PropertyRefValue statusData = null;
        private pResponseHandlingType responseAppend = pResponseHandlingType.ClearOnConnect;
        private int connState = 0;
        private int connStateW = 1;
        private TcpClient client = null;
        private static Encoding[] encoders;

        // enums
        private enum pEncodingType { BINARY, ASCII, UTF8, UTF16LE, UTF16BE, UTF32, Systemdefault }
        private enum pResponseHandlingType { AlwaysOverwrite, AlwaysAppend, ClearOnConnect }

        // my properties
        private static readonly PropertyDesc[] myprops = new PropertyDesc[]
        {
            new PropertyDesc("Hostname", StringType.Singleton, 100, "Name or IP of the remote host", "Connection"),
            new PropertyDesc("Hostport", IntegerType.Singleton, 101, "Port number of the remote host", "Connection"),
            new PropertyDesc("Connect", BooleanType.Singleton, 102, "Set to true for persistant connection", "Connection"),
            new PropertyDesc("SendTrigger", BooleanType.Singleton, 103, "Send data on false->true. Also opens a connection if \"Connect\" is false.", "Connection"),
            new PropertyDesc("SendData", StringType.Singleton, 104, "Text Data to send", "Connection"),
            new PropertyDesc("Encoding", new Enumeration(typeof(pEncodingType),false), 105, "How to encode / decode text data.", "Connection"),
            new PropertyDesc("ResponseData", PropertyRefType.Singleton, 106, "Text data we received. Must be a string variable. Response data will be discarded if ths parameter is null.", "Connection"),
            new PropertyDesc("ResponseTimeout", IntegerType.Singleton, 107, "Minimum time to wait for response data in milliseconds. We also waiting for response if \"Connect\" or \"SendTrigger\" are still true.", "Connection"),
            new PropertyDesc("ResponseAppend", new Enumeration(typeof(pResponseHandlingType), false), 108, "How to handle Responses in multiple Datapackets.", "Connection"),
            new PropertyDesc("Status", PropertyRefType.Singleton, 109, "Connection State: 0=Disconnected, 1=Connecting, 2=Connected", "Connection")
        };

        // all properties
        private static readonly PropertyDesc[] allprops = new PropertyDesc[FrameItem.FiGetNumberOfProps(false, false, false, false, false) + myprops.Length];

        protected override PropertyDesc[] GetPropertyDescriptions()
        {
            return allprops;
        }

        // constructors
        static PG2TcpClient()
        {
            encoders = new Encoding[] { Encoding.Unicode, Encoding.ASCII, Encoding.UTF8, Encoding.Unicode, Encoding.BigEndianUnicode, Encoding.UTF32, Encoding.Default };
            int n = 0;
            FrameItem.FiFillInPropertyDescriptions(allprops, ref n, false, false, false, false, false);
            foreach (PropertyDesc desc in myprops)
            {
                allprops[n++] = desc;
            }
        }

        public PG2TcpClient() : base(false, false, false)  {}

        public PG2TcpClient(PG2TcpClient other, GraphicItemVisual otherVisual, IElementView elementView) : base(other, otherVisual)
        {
            if (other != null)
            {
                host = other.host;
                port = other.port;
                connect = other.connect;
                sendTrigger = other.sendTrigger;
                sendData = other.sendData;
                encoding = other.encoding;
                responseData = PropertyRefValue.GetLeftSideValue(other.responseData, elementView);
                responseTimeout = other.responseTimeout;
                responseAppend = other.responseAppend;
                statusData = PropertyRefValue.GetLeftSideValue(other.statusData, elementView);
            }
        }

        public override GraphicItem GetRunTimeInstance(IElementView elementView, GraphicItemVisual visual)
        {
            return new PG2TcpClient(this, visual, elementView);
        }

        // called for exit
        public override void PrepareForRemoval(IElementView elementV, int itemIndex)
        {
            if (client != null)
            {
                client.Close();
                client = null;
            }
            base.PrepareForRemoval(elementV, itemIndex);
        }

        // called 60 times a second
        public void OnTimerUpdate(IElementView elementView)
        {
            if (elementView == null) return;
            switch (connState)
            {
                case 0:  // disconnected
                    if (connect || (!sendTriggerW && sendTrigger))  // if connect requested or trigger rising
                    {
                        client = new TcpClient();
                        client.ConnectAsync(host, (int)port);
                        connState = 1;
                        connectTimer = 600; // timeout 10 seconds
                        if (responseAppend == pResponseHandlingType.ClearOnConnect && responseData != null)  // clear response data
                        {
                            responseText = new byte[0];  // clear internal buffer
                            elementView.WriteProperty(new StringVariantValue(""), responseData);   // no need to decode the buffer - its empty
                        }
                    }
                    if (!sendTrigger)
                    {
                        sendTriggerW = false;
                    }
                    break;
                case 1:  // connecting
                    if (client != null && client.Connected)  // jea, we are connected!
                    {
                        connState = 2;
                    }
                    if (!connect && !sendTrigger)  // cancel connection request
                    {
                        connState = 0;
                    }
                    if (connectTimer > 0) // countdown timer
                    {
                        connectTimer--;
                    }
                    else
                    {
                        connState = 0; // timeout
                    }
                    break;
                case 2: // connected
                    if (client == null || !client.Connected) // are we disconnected ?
                    {
                        connState = 0;
                    }
                    else // we are still connected!
                    {
                        if (!sendTriggerW && sendTrigger) // we have to send something
                        {
                            byte[] b = encoders[encoding].GetBytes(sendData); // encode text
                            if (encoding == 0) // for BINARY encoding, just encode as UTF16 and copy the lower byte
                            {
                                byte[] u = new byte[b.Length / 2];
                                for (int i = 0; i < u.Length; i++)
                                {
                                    u[i] = b[i * 2];
                                }
                                b = u;
                            }
                            client.Client.Send(b);  // send text
                            sendTriggerW = true;  // mark as sent
                            responseTimer = (responseTimeout * 60) / 1000; // initialize response timeout timer
                        }

                        if (responseTimer > 0)  // countdown timer
                        {
                            responseTimer--;
                        }

                        if (!sendTrigger && !connect && responseTimer == 0)  // no need to keep this connection
                        {
                            connState = 0;
                        }

                        if (!sendTrigger && connect) // reset the trigger-flag if this is a permanent connection
                        {
                            sendTriggerW = false;
                        }
                    }

                    // handle response data
                    if (client != null && client.Available > 0)
                    {
                        byte[] b = new byte[client.Available]; // get buffer for new data
                        client.Client.Receive(b); // get the new data
                        if (responseData != null) // do PG2 need the data?
                        {
                            if (responseAppend == pResponseHandlingType.AlwaysOverwrite)  // overwrite old data
                            {
                                responseText = b;
                            }
                            else  // append data
                            {
                                byte[] o = responseText;
                                responseText = new byte[o.Length + b.Length];
                                o.CopyTo(responseText, 0);
                                b.CopyTo(responseText,o.Length);
                            }
                            // finally decode and write to PG2
                            elementView.WriteProperty(new StringVariantValue(encoders[encoding == 0 ? 1 : encoding].GetString(responseText)), responseData);
                        }
                    }
                    break;
            }

            // clear up connection
            if (connState == 0 && client != null)  
            {
                if (client.Connected)
                {
                    client.Client.Disconnect(false);
                }
                client.Close();
                client = null;
            }

            // report the connection state to PG2
            if (statusData != null && connState != connStateW)
            {
                elementView.WriteProperty(new IntVariantValue(connState), statusData);
                connStateW = connState;
            }
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
                case 102:    // property "Connect"
                    accessor.TransferBoolean(writeOperation, accessIndex, ref connect);
                    break;
                case 103:    // property "sendTrigger"
                    accessor.TransferBoolean(writeOperation, accessIndex, ref sendTrigger);
                    break;
                case 104:    // property "sendData"
                    accessor.TransferString(writeOperation, accessIndex, ref sendData);
                    break;
                case 105:    // property "encoding"
                    accessor.TransferInteger(writeOperation, accessIndex, ref encoding);
                    break;
                case 106:    // property "responseData"
                    accessor.TransferPropertyReferenceValue(writeOperation, accessIndex, ref responseData);
                    break;
                case 107:    // property "responseTimeout"
                    accessor.TransferInteger(writeOperation, accessIndex, ref responseTimeout);
                    break;
                case 108:    // property "responseAppend"
                    long r = (long)responseAppend;
                    accessor.TransferInteger(writeOperation, accessIndex, ref r);
                    responseAppend = (pResponseHandlingType)r;
                    break;
                case 109:    // property "Status"
                    accessor.TransferPropertyReferenceValue(writeOperation, accessIndex, ref statusData);
                    break;
                default:    // anything inherited
                    base.TransferValue(accessor, accessIndex, propertyIndex, writeOperation);
                    break;
            }
        }

        // disposal handling, just to satisfy the linter
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (client == null)
            {
                return;
            }
            if (disposing)
            {
                responseText = null;
            }
            client.Close();
            client = null;
        }
    }
}
