using LearningApp.Api.Entities;
using LearningApp.UI.Shared.Auth;

namespace LearningApp.Api.Authentication;

public interface IJwtTokenService
{
    LoginResponse CreateToken(User user);
}
