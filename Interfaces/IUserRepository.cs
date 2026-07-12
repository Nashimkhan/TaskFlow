using TaskFlow.Models;

namespace TaskFlow.Interfaces
{
    public interface IUserRepository
    {
        User GetByUsername(string username);

        void Add(User user);
    }
}