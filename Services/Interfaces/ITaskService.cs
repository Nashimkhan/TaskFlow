using TaskFlow.Models;

namespace TaskFlow.Services.Interfaces
{
    public interface ITaskService
    {
        List<TaskItem> GetUserTasks(
            string userId,
            string? search,
            string? status,
            string? priority);

        List<TaskItem> GetPagedTasks(
            List<TaskItem> tasks,
            int page,
            int pageSize);

        TaskItem? GetTaskById(int id, string userId);

        void CreateTask(TaskItem task, string userId);

        bool UpdateTask(TaskItem task, string userId);

        bool DeleteTask(int id, string userId);
    }
}