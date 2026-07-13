using TaskFlow.Interfaces;
using TaskFlow.Models;
using TaskFlow.Services.Interfaces;

namespace TaskFlow.Services.Implementations
{
    public class TaskService : ITaskService
    {
        private readonly ITaskRepository _taskRepository;

        public TaskService(ITaskRepository taskRepository)
        {
            _taskRepository = taskRepository;
        }

        public List<TaskItem> GetUserTasks(
            string userId,
            string? search,
            string? status,
            string? priority)
        {
            var tasks = _taskRepository.GetAll(userId);

            if (!string.IsNullOrWhiteSpace(search))
            {
                tasks = tasks
                    .Where(t =>
                        t.Title.Contains(
                            search,
                            StringComparison.OrdinalIgnoreCase) ||
                        t.Description.Contains(
                            search,
                            StringComparison.OrdinalIgnoreCase))
                    .ToList();
            }

            if (!string.IsNullOrWhiteSpace(status))
            {
                tasks = tasks
                    .Where(t => t.Status == status)
                    .ToList();
            }

            if (!string.IsNullOrWhiteSpace(priority))
            {
                tasks = tasks
                    .Where(t => t.Priority == priority)
                    .ToList();
            }

            return tasks;
        }

        public List<TaskItem> GetPagedTasks(
            List<TaskItem> tasks,
            int page,
            int pageSize)
        {
            return tasks
                .OrderByDescending(t => t.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();
        }

        public TaskItem? GetTaskById(int id, string userId)
        {
            return _taskRepository.GetById(id, userId);
        }

        public void CreateTask(TaskItem task, string userId)
        {
            task.UserId = userId;
            task.CreatedAt = DateTime.UtcNow;
            task.DueDate = ToUtc(task.DueDate);

            _taskRepository.Add(task);
        }

        public bool UpdateTask(TaskItem task, string userId)
        {
            var existingTask =
                _taskRepository.GetById(task.Id, userId);

            if (existingTask == null)
            {
                return false;
            }

            existingTask.Title = task.Title;
            existingTask.Description = task.Description;
            existingTask.Status = task.Status;
            existingTask.Priority = task.Priority;
            existingTask.DueDate = ToUtc(task.DueDate);

            _taskRepository.Update(existingTask);

            return true;
        }

        public bool DeleteTask(int id, string userId)
        {
            var task = _taskRepository.GetById(id, userId);

            if (task == null)
            {
                return false;
            }

            _taskRepository.Delete(task);

            return true;
        }

        private static DateTime ToUtc(DateTime date)
        {
            return DateTime.SpecifyKind(
                date,
                DateTimeKind.Utc);
        }
    }
}