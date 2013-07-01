﻿#region Header

// Copyright (c) 2010, Hexasystems Corporation
// All rights reserved.

#endregion Header

namespace netDumbster.smtp
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using netDumbster.smtp;
    using System.Net;

    public class RawSmtpMessage
    {
        List<EmailAddress> recipients;

        public RawSmtpMessage(IPAddress localIPAddress, int localPort, IPAddress remoteIPAddress, int remotePort)
        {
            this.Data = new StringBuilder();
            this.recipients = new List<EmailAddress>();
            this.LocalIPAddress = localIPAddress;
            this.LocalPort = localPort;
            this.RemoteIPAddress = remoteIPAddress;
            this.RemotePort = remotePort;
        }
        
        public StringBuilder Data
        {
            get;
            private set;
        }

        public IEnumerable<EmailAddress> Recipients
        {
            get
            {
                return this.recipients;
            }
        }

        public void AddRecipient(EmailAddress recipient)
        {
            this.recipients.Add(recipient);
        }

        /// <summary>
        /// Gets the remote IP address.
        /// </summary>
        public IPAddress RemoteIPAddress
        {
            get;
            private set;
        }

        /// <summary>
        /// Gets the local IP address.
        /// </summary>
        public IPAddress LocalIPAddress
        {
            get;
            private set;
        }

        /// <summary>
        /// Gets the remote port.
        /// </summary>
        public int RemotePort
        {
            get;
            private set;
        }

        /// <summary>
        /// Gets the local port.
        /// </summary>
        public int LocalPort
        {
            get;
            private set;
        }
    }
}
