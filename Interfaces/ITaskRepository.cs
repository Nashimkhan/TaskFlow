using TaskFlow.Models;

namespace TaskFlow.Interfaces
{
    public interface ITaskRepository
    {
        List<TaskItem> GetAll(int userId);

        TaskItem GetById(int id, int userId);

        List<TaskItem> Search(int userId, string query);

        void Add(TaskItem task);

        void Update(TaskItem task);

        void Delete(TaskItem task);


    }
}