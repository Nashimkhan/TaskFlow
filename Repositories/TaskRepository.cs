using TaskFlow.Data;
using TaskFlow.Interfaces;
using TaskFlow.Models;

namespace TaskFlow.Repositories
{
    public class TaskRepository : ITaskRepository
    {
        private readonly AppDbContext _context;

        public TaskRepository(AppDbContext context)
        {
            _context = context;
        }

        public List<TaskItem> GetAll(string userId)
        {
            return _context.Tasks
                .Where(t => t.UserId == userId)
                .ToList();
        }

        public TaskItem? GetById(int id, string userId)
        {
            return _context.Tasks
                .FirstOrDefault(t => t.Id == id && t.UserId == userId);
        }

        public List<TaskItem> Search(string userId, string query)
        {
            return _context.Tasks
                .Where(t => t.UserId == userId &&
                    (t.Title.ToLower().Contains(query.ToLower()) ||
                     t.Description.ToLower().Contains(query.ToLower())))
                .ToList();
        }

        public void Add(TaskItem task)
        {
            _context.Tasks.Add(task);
            _context.SaveChanges();
        }

        public void Update(TaskItem task)
        {
            _context.Tasks.Update(task);
            _context.SaveChanges();
        }

        public void Delete(TaskItem task)
        {
            _context.Tasks.Remove(task);
            _context.SaveChanges();
        }
    }
}