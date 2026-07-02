using CureFlow.Application.Interfaces;

namespace CureFlow.Infrastructure.Identity;

public class BcryptPasswordHasher : IPasswordHasher
{
    public string Hash(string password) => BCrypt.Net.BCrypt.HashPassword(password, workFactor: 11);

    public bool Verify(string password, string hash)
    {
        return !string.IsNullOrWhiteSpace(hash) && BCrypt.Net.BCrypt.Verify(password, hash);
    }
}
