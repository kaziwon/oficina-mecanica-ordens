using OficinaMecanica.Ordens.Application.Outputs.Auth;
namespace OficinaMecanica.Ordens.Application.Inputs.Auth;

public class LoginInput
{
    public LoginInput(string username, string password)
    {
        Username = username;
        Password = password;
    }

    public string Username { get; }
    public string Password { get; }
}
