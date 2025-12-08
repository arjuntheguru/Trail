using System;
using System.Threading.Tasks;
using Trail.Domain.Entities;

namespace Trail.Application.Common.Interfaces
{
    public interface ITaskManager
    {
        void RunTasks();
        Task CreateCalendarEntryForTaskAllocation(TaskAllocation taskAllocation, DateTime targetDate);
    }
}