using TaskFlow.Models;

namespace TaskFlow.Interfaces
{
    public interface ITaskRepository
    {
        List<TaskItem> GetAll(string userId);

        TaskItem? GetById(int id, string userId);

        List<TaskItem> Search(string userId, string query);

        void Add(TaskItem task);

        void Update(TaskItem task);

        void Delete(TaskItem task);
    }
}