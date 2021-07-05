using System;
using System.Collections.Generic;
using System.Text;

namespace Trail.Application.Common.Interfaces
{
    public interface IEmailService
    {
        void Send(string from, string to, string subject, string html);
    }
}
